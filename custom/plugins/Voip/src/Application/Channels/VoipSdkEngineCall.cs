using Callora.Plugin.Communication.Abstractions;
using Callora.Plugins.Voip.Application.Audio;
using CalloraVoipSdk.Core.Application.Media;
using SdkCall = CalloraVoipSdk.Core.Domain.Calls.ICall;
using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;
using SdkDtmfTone = CalloraVoipSdk.Core.Domain.Calls.DtmfTone;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Wraps one CalloraVoipSdk call behind the narrow engine-call port.
/// </summary>
public sealed class VoipSdkEngineCall : IEngineCall
{
    private readonly SdkCall _inner;
    private readonly MediaManager _media;

    public VoipSdkEngineCall(SdkCall inner, MediaManager media)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(media);
        _inner = inner;
        _media = media;
        _inner.StateChanged += HandleInnerStateChanged;
    }

    private void HandleInnerStateChanged(
        object? sender,
        CalloraVoipSdk.Core.Domain.Events.CallStateChangedEventArgs args)
    {
        StateChanged?.Invoke(args.NewState);

        if (args.NewState == SdkCallState.Terminated)
        {
            // Terminated is final: detach so long-lived SDK call objects
            // cannot pin adapter instances.
            _inner.StateChanged -= HandleInnerStateChanged;
        }
    }

    public string CallId => _inner.CallId.ToString();

    public SdkCallState State => _inner.State;

    public SdkCallDirection Direction => _inner.Direction;

    public string RemoteParty => _inner.RemoteParty;

    public event Action<SdkCallState>? StateChanged;

    public Task AcceptAsync(CancellationToken cancellationToken = default) =>
        _inner.AcceptAsync(cancellationToken);

    public async Task RejectAsync(CancellationToken cancellationToken = default)
    {
        // The SDK reports foreseeable reject outcomes via the result instead of
        // throwing; the platform contract expects an exception on failure.
        var result = await _inner.RejectAsync(ct: cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Rejecting call '{CallId}' failed with status '{result.Status}': {result.Reason}");
        }
    }

    public Task HangupAsync(CancellationToken cancellationToken = default) =>
        _inner.HangupAsync(cancellationToken);

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) =>
        _inner.SendDtmfAsync(new SdkDtmfTone(tone), cancellationToken);

    public Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default)
    {
        var parameters = _inner.MediaParameters
            ?? throw new InvalidOperationException(
                $"Call '{CallId}' has no negotiated media yet; audio requires a connected call.");

        var receiver = _media.CreateReceiver();
        var sender = _media.CreateSender();
        receiver.AttachToCall(_inner);
        sender.AttachToCall(_inner);

        var format = new AudioFormat(parameters.CodecName, parameters.ClockRate);
        return Task.FromResult<ICallAudioStream>(
            new VoipSdkAudioStream(receiver, sender, format, parameters.PayloadType));
    }
}
