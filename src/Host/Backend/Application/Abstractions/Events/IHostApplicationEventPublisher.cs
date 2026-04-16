namespace Callora.Host.Backend.Application.Abstractions.Events;

public interface IHostApplicationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostApplicationEvent;
}
