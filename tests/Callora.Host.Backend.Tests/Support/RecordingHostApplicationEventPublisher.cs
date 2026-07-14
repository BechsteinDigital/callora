using Callora.Host.Backend.Application.Events;
using Callora.Host.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class RecordingHostApplicationEventPublisher : IHostApplicationEventPublisher
{
    private readonly List<IHostApplicationEvent> _events = [];

    public IReadOnlyList<IHostApplicationEvent> Events => _events;

    public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostEvent
    {
        if (appEvent is not IHostApplicationEvent hostEvent)
        {
            throw new InvalidOperationException(
                $"Unsupported event type '{typeof(TEvent).FullName}' for {nameof(RecordingHostApplicationEventPublisher)}.");
        }

        _events.Add(hostEvent);
        return Task.CompletedTask;
    }
}
