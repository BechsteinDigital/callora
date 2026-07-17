using Callora.Core.Application.Events.Contracts;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Events;

/// <summary>
/// Base class for cancelable/mutable "before" host events — the Callora counterpart of
/// Umbraco's cancelable notifications. Subscribers intervene through the inherited
/// <see cref="InterceptableEvent.State"/>, <see cref="InterceptableEvent.StopPropagation"/>
/// and <see cref="InterceptableEvent.Cancel"/> members; the code that raised the event
/// checks <see cref="InterceptableEvent.IsCanceled"/> afterwards.
/// </summary>
/// <remarks>
/// By convention a mutable event is named with an "-ing" suffix (e.g. WorkspaceCreating) and
/// paired with a read-only "-ed" event dispatched once the operation completed. Existing
/// read-only events keep reacting through the same dispatcher; adopting this base is opt-in.
/// </remarks>
[CalloraExtensible("Base for cancelable/mutable before host-events that plugins can observe and veto (REV2 §8.2)")]
public abstract class MutableHostEvent : InterceptableEvent, IHostApplicationEvent, IHostApplicationEventPropagationState
{
    /// <summary>Creates the event with the instant it was raised.</summary>
    /// <param name="occurredAtUtc">When the event was raised.</param>
    protected MutableHostEvent(DateTimeOffset occurredAtUtc) => OccurredAtUtc = occurredAtUtc;

    /// <inheritdoc />
    public DateTimeOffset OccurredAtUtc { get; }
}
