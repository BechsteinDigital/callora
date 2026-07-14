using Callora.Plugin.Communication.Abstractions;
using Callora.Plugins.Voip.Application.Audio;
using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Adapts one engine call onto the platform call contract. Pure delegation
/// plus state mapping; no SIP behavior is duplicated here.
/// </summary>
public sealed class SipCall : IVoipCall
{
    private readonly IEngineCall _inner;
    private readonly object _stateLock = new();
    private CallState _state;

    public SipCall(IEngineCall inner, CallTarget target)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(target);

        _inner = inner;
        Target = target;
        CallId = inner.CallId;
        Direction = inner.Direction == SdkCallDirection.Inbound
            ? CallDirection.Inbound
            : CallDirection.Outbound;
        _state = SipCallStateMapper.Map(inner.State);
        _inner.StateChanged += HandleEngineStateChanged;
    }

    public string CallId { get; }

    public CallState State
    {
        get
        {
            lock (_stateLock)
            {
                return _state;
            }
        }
    }

    public CallDirection Direction { get; }

    public CallTarget Target { get; }

    public event EventHandler<CallStateChangedEventArgs>? StateChanged;

    public Task AcceptAsync(CancellationToken cancellationToken = default) =>
        _inner.AcceptAsync(cancellationToken);

    public Task RejectAsync(CancellationToken cancellationToken = default) =>
        _inner.RejectAsync(cancellationToken);

    public Task HangupAsync(CancellationToken cancellationToken = default) =>
        _inner.HangupAsync(cancellationToken);

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) =>
        _inner.SendDtmfAsync(tone, cancellationToken);

    public Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default) =>
        _inner.OpenAudioAsync(cancellationToken);

    private void HandleEngineStateChanged(SdkCallState engineState)
    {
        CallStateChangedEventArgs? payload;
        var reachedTerminated = false;
        lock (_stateLock)
        {
            var mapped = SipCallStateMapper.Map(engineState);
            if (mapped == _state || _state == CallState.Terminated)
                return;

            payload = new CallStateChangedEventArgs(_state, mapped);
            _state = mapped;
            reachedTerminated = mapped == CallState.Terminated;
        }

        StateChanged?.Invoke(this, payload);

        if (reachedTerminated)
        {
            // Terminated is final: detach so long-lived engine objects cannot
            // pin completed adapter instances.
            _inner.StateChanged -= HandleEngineStateChanged;
        }
    }
}
