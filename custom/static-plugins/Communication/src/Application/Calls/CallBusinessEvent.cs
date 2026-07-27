using System.Globalization;
using Callora.Core.Application.Events.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// A call lifecycle business event (placed/state-changed/ended), published to the business-event bus so
/// flows, webhooks and in-process plugin listeners react to calls — e.g. a CRM screen-pop or an AI agent
/// attaching to the media stream. The event names come from <see cref="CallEventTypes"/>.
/// </summary>
public sealed class CallBusinessEvent : IBusinessEvent
{
    private readonly string _callId;
    private readonly CallDirection _direction;
    private readonly string _remoteParty;
    private readonly CallState _state;
    private readonly DateTimeOffset _at;

    private CallBusinessEvent(
        string eventName,
        string workspaceKey,
        string callId,
        CallDirection direction,
        string remoteParty,
        CallState state,
        DateTimeOffset at)
    {
        EventName = eventName;
        WorkspaceKey = workspaceKey;
        _callId = callId;
        _direction = direction;
        _remoteParty = remoteParty;
        _state = state;
        _at = at;
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string? WorkspaceKey { get; }

    /// <summary>An inbound call is ringing on a channel (not yet answered).</summary>
    public static CallBusinessEvent Ringing(
        string workspaceKey, string callId, CallDirection direction, string remoteParty, CallState state, DateTimeOffset at) =>
        new(CallEventTypes.Ringing, workspaceKey, callId, direction, remoteParty, state, at);

    /// <summary>An outbound call was placed and is being established.</summary>
    public static CallBusinessEvent Placed(
        string workspaceKey, string callId, CallDirection direction, string remoteParty, CallState state, DateTimeOffset at) =>
        new(CallEventTypes.Placed, workspaceKey, callId, direction, remoteParty, state, at);

    /// <summary>A tracked call changed lifecycle state (e.g. connected).</summary>
    public static CallBusinessEvent StateChanged(
        string workspaceKey, string callId, CallDirection direction, string remoteParty, CallState state, DateTimeOffset at) =>
        new(CallEventTypes.StateChanged, workspaceKey, callId, direction, remoteParty, state, at);

    /// <summary>A tracked call ended and will not change state again.</summary>
    public static CallBusinessEvent Ended(
        string workspaceKey, string callId, CallDirection direction, string remoteParty, DateTimeOffset at) =>
        new(CallEventTypes.Ended, workspaceKey, callId, direction, remoteParty, CallState.Terminated, at);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["callId"] = _callId,
        ["workspaceKey"] = WorkspaceKey ?? string.Empty,
        ["direction"] = _direction.ToString(),
        ["remoteParty"] = _remoteParty,
        ["state"] = _state.ToString(),
        ["at"] = _at.ToString("O", CultureInfo.InvariantCulture),
    };
}
