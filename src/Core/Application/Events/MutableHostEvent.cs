using Callora.Core.Application.Events.Contracts;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Events;

/// <summary>
/// Base class for "before" host events that let subscribers intervene, not just react —
/// the Callora counterpart of Umbraco's cancelable notifications. Subscribers can share
/// data through <see cref="State"/>, skip the remaining subscribers via
/// <see cref="StopPropagation"/>, and veto the operation via <see cref="Cancel"/>. The code
/// that publishes the event inspects <see cref="IsCanceled"/> afterwards and aborts the
/// operation when it is set.
/// </summary>
/// <remarks>
/// By convention a mutable event is named with an "-ing" suffix (e.g. WorkspaceCreating) and
/// paired with a read-only "-ed" event dispatched once the operation has completed. Existing
/// read-only events keep reacting through the same dispatcher; adopting this base is opt-in.
/// </remarks>
[CalloraExtensible("Base for cancelable/mutable before-events that plugins can observe and veto (REV2 §8.2)")]
public abstract class MutableHostEvent : IHostApplicationEvent, IHostApplicationEventPropagationState
{
    private Dictionary<string, object?>? _state;

    /// <summary>Creates the event with the instant it was raised.</summary>
    /// <param name="occurredAtUtc">When the event was raised.</param>
    protected MutableHostEvent(DateTimeOffset occurredAtUtc) => OccurredAtUtc = occurredAtUtc;

    /// <inheritdoc />
    public DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// Mutable bag for passing data between the subscribers of this event, e.g. a subscriber
    /// enriches a value that a later one reads. Allocated on first access.
    /// </summary>
    public IDictionary<string, object?> State => _state ??= [];

    /// <inheritdoc />
    public bool IsPropagationStopped { get; private set; }

    /// <summary>
    /// True once a subscriber has vetoed the operation via <see cref="Cancel"/>. The publisher
    /// of the event checks this after dispatch and aborts the operation when set.
    /// </summary>
    public bool IsCanceled { get; private set; }

    /// <summary>Skips the remaining subscribers for this event; those already run are unaffected.</summary>
    public void StopPropagation() => IsPropagationStopped = true;

    /// <summary>
    /// Vetoes the operation. Remaining subscribers still run and can observe
    /// <see cref="IsCanceled"/>; call <see cref="StopPropagation"/> as well to skip them.
    /// </summary>
    public void Cancel() => IsCanceled = true;
}
