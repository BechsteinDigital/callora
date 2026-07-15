using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Tests.Infrastructure.Events.Support;

internal sealed class RecordingPublishDecorator(
    string name,
    int decorationPriority) : IHostApplicationEventPublisherDecorator
{
    public int DecorationPriority { get; } = decorationPriority;

    public async Task PublishAsync<TEvent>(
        TEvent appEvent,
        HostApplicationEventPublishNext<TEvent> next,
        CancellationToken cancellationToken = default)
        where TEvent : IHostEvent
    {
        if (appEvent is not PublishPipelineTestEvent publishEvent)
        {
            await next(appEvent, cancellationToken);
            return;
        }

        publishEvent.Steps.Add($"{name}.before");
        await next(appEvent, cancellationToken);
        publishEvent.Steps.Add($"{name}.after");
    }
}
