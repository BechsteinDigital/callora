using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events.Support;

internal sealed class RecordingPublishPipelineDispatcher : IHostApplicationEventDispatcher
{
    public Task DispatchAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostEvent
    {
        if (appEvent is PublishPipelineTestEvent publishEvent)
        {
            publishEvent.Steps.Add("dispatch");
        }

        return Task.CompletedTask;
    }
}
