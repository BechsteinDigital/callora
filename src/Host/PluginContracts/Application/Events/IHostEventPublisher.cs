namespace Callora.Host.PluginContracts.Application.Events;

/// <summary>
/// Publishes one host event instance to all matching subscribers.
/// </summary>
public interface IHostEventPublisher
{
    /// <summary>
    /// Publishes one event.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostEvent;
}
