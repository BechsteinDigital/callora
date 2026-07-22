using Callora.Plugin.Communication.Abstractions;
using CalloraVoipSdk.Core.Application.Media;
using CallActionStatus = CalloraVoipSdk.Core.Domain.Calls.CallActionStatus;
using NativeCall = CalloraVoipSdk.Core.Domain.Calls.ICall;
using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;
using SdkCallStateChangedEventArgs = CalloraVoipSdk.Core.Domain.Events.CallStateChangedEventArgs;
using SdkDtmfTone = CalloraVoipSdk.Core.Domain.Calls.DtmfTone;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Adapts a CalloraVoipSdk call (<see cref="NativeCall"/>) to the foundation's modality-neutral
/// <see cref="IVoipCall"/>. It maps the SDK's richer lifecycle onto the four foundation states,
/// forwards the four foundation actions to the SDK, and opens the audio bridge (B4-deep-1) on
/// demand. SDK-only richness (hold, transfer, ICE, quality) stays behind the adapter — foundation
/// consumers see only the neutral contract.
/// </summary>
public sealed class SdkCall : IVoipCall
{
    private readonly NativeCall _sdkCall;
    private readonly Func<(IMediaReceiver Receiver, IMediaSender Sender)> _mediaTapFactory;

    /// <summary>
    /// Wraps one live SDK call. <paramref name="mediaTapFactory"/> creates the per-call receiver/sender
    /// tap that <see cref="OpenAudioAsync"/> attaches to the call — injected so the adapter need not
    /// depend on the SDK media manager directly (and stays unit-testable).
    /// </summary>
    public SdkCall(NativeCall sdkCall, Func<(IMediaReceiver Receiver, IMediaSender Sender)> mediaTapFactory)
    {
        ArgumentNullException.ThrowIfNull(sdkCall);
        ArgumentNullException.ThrowIfNull(mediaTapFactory);

        _sdkCall = sdkCall;
        _mediaTapFactory = mediaTapFactory;
        _sdkCall.StateChanged += OnSdkStateChanged;
    }

    /// <inheritdoc />
    public string CallId => _sdkCall.CallId.ToString();

    /// <inheritdoc />
    public CallState State => MapState(_sdkCall.State);

    /// <inheritdoc />
    public CallDirection Direction => MapDirection(_sdkCall.Direction);

    /// <inheritdoc />
    public CallTarget Target => new(_sdkCall.RemoteParty);

    /// <inheritdoc />
    public event EventHandler<CallStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public Task AcceptAsync(CancellationToken cancellationToken = default) =>
        _sdkCall.AcceptAsync(cancellationToken);

    /// <inheritdoc />
    public async Task RejectAsync(CancellationToken cancellationToken = default)
    {
        // The SDK reports foreseeable reject outcomes as a result rather than throwing; translate them
        // back to the foundation's throwing contract (InvalidState → InvalidOperationException).
        var result = await _sdkCall.RejectAsync(ct: cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return;
        }

        if (result.Status == CallActionStatus.Canceled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        throw new InvalidOperationException(result.Reason ?? $"Reject failed: {result.Status}.");
    }

    /// <inheritdoc />
    public Task HangupAsync(CancellationToken cancellationToken = default) =>
        _sdkCall.HangupAsync(cancellationToken);

    /// <inheritdoc />
    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) =>
        _sdkCall.SendDtmfAsync(new SdkDtmfTone(tone), cancellationToken);

    /// <inheritdoc />
    public Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (receiver, sender) = _mediaTapFactory();
        try
        {
            receiver.AttachToCall(_sdkCall);
            sender.AttachToCall(_sdkCall);
        }
        catch
        {
            receiver.Dispose();
            sender.Dispose();
            throw;
        }

        return Task.FromResult<ICallAudioStream>(new SdkCallAudioStream(receiver, sender));
    }

    private void OnSdkStateChanged(object? sender, SdkCallStateChangedEventArgs e)
    {
        var previous = MapState(e.OldState);
        var current = MapState(e.NewState);

        // Several SDK states collapse onto Connected (OnHold/Transferring); suppress the mapped no-ops
        // so foundation consumers see only real transitions in order.
        if (previous != current)
        {
            StateChanged?.Invoke(this, new CallStateChangedEventArgs(previous, current));
        }

        // No SDK events follow Terminated — detach so the adapter does not outlive the call.
        if (current == CallState.Terminated)
        {
            _sdkCall.StateChanged -= OnSdkStateChanged;
        }
    }

    private static CallState MapState(SdkCallState state) => state switch
    {
        SdkCallState.Idle or SdkCallState.Dialing => CallState.Connecting,
        SdkCallState.Ringing => CallState.Ringing,
        SdkCallState.Connected or SdkCallState.OnHold or SdkCallState.Transferring => CallState.Connected,
        SdkCallState.Terminated => CallState.Terminated,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown SDK call state."),
    };

    private static CallDirection MapDirection(SdkCallDirection direction) => direction switch
    {
        SdkCallDirection.Inbound => CallDirection.Inbound,
        SdkCallDirection.Outbound => CallDirection.Outbound,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown SDK call direction."),
    };
}
