using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Application.Abstractions.Events;

/// <summary>
/// Dispatches host application events to their registered subscribers.
/// </summary>
public interface IHostApplicationEventDispatcher
{
    /// <summary>
    /// Dispatches one event instance to all matching subscribers.
    /// </summary>
    Task DispatchAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostEvent;
}
