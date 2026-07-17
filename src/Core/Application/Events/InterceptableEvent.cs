using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Events;

/// <summary>
/// Shared base for "before" events that let subscribers intervene, not just react — the
/// mechanism behind <see cref="MutableHostEvent"/> and <see cref="MutableBusinessEvent"/>.
/// Subscribers share data through <see cref="State"/>, skip the rest via
/// <see cref="StopPropagation"/>, and veto the operation via <see cref="Cancel"/>. The
/// dispatcher honours <see cref="IsPropagationStopped"/>; the code that raised the event
/// inspects <see cref="IsCanceled"/> afterwards and aborts the operation when it is set.
/// </summary>
/// <remarks>
/// Plugins derive from the concrete <see cref="MutableHostEvent"/> or
/// <see cref="MutableBusinessEvent"/> bases rather than this type directly.
/// </remarks>
public abstract class InterceptableEvent : IHostEvent, IHostEventPropagationState
{
    private Dictionary<string, object?>? _state;

    /// <summary>
    /// Mutable bag for passing data between the subscribers of this event; a subscriber can
    /// enrich a value that a later one reads. Allocated on first access.
    /// </summary>
    public IDictionary<string, object?> State => _state ??= [];

    /// <inheritdoc />
    public bool IsPropagationStopped { get; private set; }

    /// <summary>
    /// True once a subscriber has vetoed the operation via <see cref="Cancel"/>. The code that
    /// raised the event checks this after dispatch and aborts the operation when set.
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
