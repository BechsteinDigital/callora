using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Events;

/// <summary>
/// Host-registered subscriber for one host event type. Invoked by the dispatcher
/// in priority order alongside plugin-exported subscribers of the same event.
/// </summary>
/// <typeparam name="TEvent">The host event type this subscriber handles.</typeparam>
public interface IHostApplicationEventSubscriber<in TEvent>
    where TEvent : IHostEvent
{
    /// <summary>
    /// Handles one occurrence of the event. A thrown exception is logged and does
    /// not stop the remaining subscribers.
    /// </summary>
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
