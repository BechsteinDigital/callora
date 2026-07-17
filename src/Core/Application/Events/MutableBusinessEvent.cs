using Callora.Core.Application.Events.Contracts;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Events;

/// <summary>
/// Base class for cancelable/mutable "before" business events. Where a read-only
/// <see cref="IBusinessEvent"/> is a post-hoc notification (fans out to flows, webhooks and
/// listeners), a mutable business event lets listeners intervene before the operation
/// commits: they share data through <see cref="InterceptableEvent.State"/>, skip the rest via
/// <see cref="InterceptableEvent.StopPropagation"/>, and veto via
/// <see cref="InterceptableEvent.Cancel"/>. The publisher inspects
/// <see cref="InterceptableEvent.IsCanceled"/> after <c>IBusinessEventBus.PublishAsync</c>
/// and aborts the operation when set.
/// </summary>
/// <remarks>
/// By convention named with an "-ing" event name (e.g. "call.starting"), paired with a
/// read-only "-ed" event once the operation completed. Adopting this base is opt-in;
/// existing read-only business events are unaffected.
/// </remarks>
[CalloraExtensible("Base for cancelable/mutable before business-events that plugins can observe and veto (REV2 §8.2)")]
public abstract class MutableBusinessEvent : InterceptableEvent, IBusinessEvent
{
    /// <summary>Creates the event with its name and optional workspace scope.</summary>
    /// <param name="eventName">Stable dotted event name consumers subscribe by.</param>
    /// <param name="workspaceKey">Workspace the event belongs to; null for platform-wide.</param>
    protected MutableBusinessEvent(string eventName, string? workspaceKey)
    {
        EventName = eventName;
        WorkspaceKey = workspaceKey;
    }

    /// <inheritdoc />
    public string EventName { get; }

    /// <inheritdoc />
    public string? WorkspaceKey { get; }

    /// <inheritdoc />
    public abstract IReadOnlyDictionary<string, string> ToEventData();
}
