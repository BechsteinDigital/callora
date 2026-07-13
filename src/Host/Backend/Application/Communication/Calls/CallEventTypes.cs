namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// Stable event type codes published over the call event stream.
/// </summary>
public static class CallEventTypes
{
    public const string Ringing = "call.ringing";
    public const string Placed = "call.placed";
    public const string StateChanged = "call.state-changed";
    public const string Ended = "call.ended";
    public const string ConsentGranted = "call.consent-granted";
    public const string ConsentDenied = "call.consent-denied";
}
