using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Core.Application.Events;

public interface IHostApplicationEventSubscriber<in TEvent>
    where TEvent : IHostEvent
{
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
