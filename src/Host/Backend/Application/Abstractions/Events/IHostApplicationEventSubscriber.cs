using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Application.Abstractions.Events;

public interface IHostApplicationEventSubscriber<in TEvent>
    where TEvent : IHostEvent
{
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
