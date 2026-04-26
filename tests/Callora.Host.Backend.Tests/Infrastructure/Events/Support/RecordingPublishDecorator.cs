using Callora.Host.Backend.Application.Abstractions.Events;
using VoipHost.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events.Support;

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
            await next(appEvent, cancellationToken).ConfigureAwait(false);
            return;
        }

        publishEvent.Steps.Add($"{name}.before");
        await next(appEvent, cancellationToken).ConfigureAwait(false);
        publishEvent.Steps.Add($"{name}.after");
    }
}
