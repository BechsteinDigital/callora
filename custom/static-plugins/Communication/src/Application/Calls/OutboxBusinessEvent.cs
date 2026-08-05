using Callora.Core.Application.Events.Contracts;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// A stored call event on its way back onto the bus (#113). The drainer republishes the payload
/// the original event carried, so an out-of-process consumer receives exactly what an in-process
/// listener would have seen at the moment of the transition.
/// </summary>
/// <param name="eventName">Business-event name, for example <c>call.ended</c>.</param>
/// <param name="workspaceKey">Workspace the event belongs to.</param>
/// <param name="data">The event data as it was serialized at transition time.</param>
internal sealed class OutboxBusinessEvent(
    string eventName,
    string? workspaceKey,
    IReadOnlyDictionary<string, string> data) : IBusinessEvent
{
    /// <inheritdoc />
    public string EventName { get; } = eventName;

    /// <inheritdoc />
    public string? WorkspaceKey { get; } = workspaceKey;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> ToEventData() => data;
}
