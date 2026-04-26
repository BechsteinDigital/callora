using VoipHost.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events.Support;

internal sealed class CallbackPluginEventSubscriber<TEvent>(
    int priority,
    Action<TEvent> callback) : IHostEventSubscriber<TEvent>
    where TEvent : IHostEvent
{
    public int Priority { get; } = priority;

    public Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default)
    {
        callback(appEvent);
        return Task.CompletedTask;
    }
}
