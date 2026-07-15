using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Events;

public interface IHostApplicationEventSubscriber<in TEvent>
    where TEvent : IHostEvent
{
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
