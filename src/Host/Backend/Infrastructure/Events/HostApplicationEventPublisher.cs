using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Infrastructure.Events;

/// <summary>
/// Publishes host events through a decorator pipeline and a concrete dispatcher.
/// </summary>
public sealed class HostApplicationEventPublisher(
    IHostApplicationEventDispatcher dispatcher,
    IEnumerable<IHostApplicationEventPublisherDecorator> decorators) : IHostApplicationEventPublisher
{
    private readonly IHostApplicationEventPublisherDecorator[] _orderedDecorators = decorators
        .OrderBy(x => x.DecorationPriority)
        .ToArray();

    public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostEvent
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        HostApplicationEventPublishNext<TEvent> pipeline = dispatcher.DispatchAsync;

        foreach (var decorator in _orderedDecorators)
        {
            var next = pipeline;
            pipeline = (eventToPublish, token) => decorator.PublishAsync(eventToPublish, next, token);
        }

        return pipeline(appEvent, cancellationToken);
    }
}
