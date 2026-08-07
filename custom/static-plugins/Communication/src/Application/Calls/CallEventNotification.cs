using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One call lifecycle transition, as a live client sees it.
/// </summary>
/// <param name="EventName">The <c>call.*</c> event this transition raised.</param>
/// <param name="WorkspaceKey">Owning workspace; the stream is filtered on it.</param>
/// <param name="CallId">The call that changed.</param>
/// <param name="Direction">Call direction, as a stable string.</param>
/// <param name="State">Lifecycle state after the transition, as a stable string.</param>
/// <param name="RemoteParty">Remote participant address.</param>
/// <param name="OccurredAt">When the transition happened.</param>
/// <param name="InboundIdentity">
/// Who called and which number they reached, on an inbound call. Carried because the active list
/// carries it too — the stream must not show something a reader of that list could not see.
/// </param>
/// <remarks>
/// Deliberately the same information <c>GET calls/active</c> returns, plus the event name. A client
/// that fetches the active list and then follows this stream has one consistent picture, and the
/// stream carries nothing a reader of that list could not already see.
/// </remarks>
public sealed record CallEventNotification(
    string EventName,
    string WorkspaceKey,
    string CallId,
    string Direction,
    string State,
    string RemoteParty,
    DateTimeOffset OccurredAt,
    InboundCallIdentity? InboundIdentity = null)
{
    /// <summary>Builds the notification for one transition, projecting the enums to stable strings.</summary>
    public static CallEventNotification For(
        string eventName,
        string workspaceKey,
        string callId,
        CallDirection direction,
        CallState state,
        string remoteParty,
        DateTimeOffset occurredAt,
        InboundCallIdentity? inboundIdentity = null) =>
        new(
            eventName, workspaceKey, callId, direction.ToString(), state.ToString(), remoteParty,
            occurredAt, inboundIdentity);
}
