using Callora.Plugin.Communication.Abstractions;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Fake call that also exposes recording-consent, so the hub's consent-handler
/// lifecycle can be exercised. <see cref="ConsentSubscriberCount"/> reveals
/// whether the hub still holds a handler after termination (audit finding H5).
/// </summary>
public sealed class ConsentAwareStaticCall : ICall, IRecordingConsentCall
{
    private CallState _state;
    private RecordingConsentState _consentState = RecordingConsentState.NotRequested;

    public ConsentAwareStaticCall(CallTarget target, CallState initialState = CallState.Connected)
    {
        CallId = Guid.NewGuid().ToString("N");
        Target = target;
        _state = initialState;
    }

    public string CallId { get; }

    public CallState State => _state;

    public CallDirection Direction => CallDirection.Outbound;

    public CallTarget Target { get; }

    public RecordingConsentState ConsentState => _consentState;

    public event EventHandler<CallStateChangedEventArgs>? StateChanged;

    public event EventHandler<RecordingConsentChangedEventArgs>? ConsentChanged;

    /// <summary>Live number of handlers attached to <see cref="ConsentChanged"/>.</summary>
    public int ConsentSubscriberCount => ConsentChanged?.GetInvocationList().Length ?? 0;

    public void TransitionTo(CallState newState)
    {
        var previous = _state;
        if (previous == newState || previous == CallState.Terminated)
        {
            return;
        }

        _state = newState;
        StateChanged?.Invoke(this, new CallStateChangedEventArgs(previous, newState));
    }

    public void RaiseConsent(RecordingConsentState newState)
    {
        var previous = _consentState;
        _consentState = newState;
        ConsentChanged?.Invoke(this, new RecordingConsentChangedEventArgs(previous, newState));
    }

    public Task AcceptAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RejectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task HangupAsync(CancellationToken cancellationToken = default)
    {
        TransitionTo(CallState.Terminated);
        return Task.CompletedTask;
    }

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<RecordingConsentResult> RequestRecordingConsentAsync(
        RecordingConsentRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(RecordingConsentResult.Granted);
}
