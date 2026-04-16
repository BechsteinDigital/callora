namespace Callora.Host.Backend.Application.Abstractions.Events;

public interface IHostApplicationEventSubscriber<in TEvent>
    where TEvent : IHostApplicationEvent
{
    Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default);
}
