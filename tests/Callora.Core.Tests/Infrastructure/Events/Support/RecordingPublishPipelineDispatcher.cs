using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Tests.Infrastructure.Events.Support;

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
