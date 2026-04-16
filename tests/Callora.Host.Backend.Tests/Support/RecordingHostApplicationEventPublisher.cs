using Callora.Host.Backend.Application.Abstractions.Events;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class RecordingHostApplicationEventPublisher : IHostApplicationEventPublisher
{
    private readonly List<IHostApplicationEvent> _events = [];

    public IReadOnlyList<IHostApplicationEvent> Events => _events;

    public Task PublishAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostApplicationEvent
    {
        _events.Add(appEvent);
        return Task.CompletedTask;
    }
}
