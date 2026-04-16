using Callora.Host.Backend.Application.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Infrastructure.Events;

public sealed class HostApplicationEventPublisher(IServiceProvider services) : IHostApplicationEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostApplicationEvent
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        var handlers = services.GetServices<IHostApplicationEventSubscriber<TEvent>>();
        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler.HandleAsync(appEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
