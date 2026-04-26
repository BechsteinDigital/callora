using Callora.Host.Backend.Application.Abstractions.Events;
using VoipHost.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events.Support;

internal sealed class CallbackHostApplicationEventSubscriber<TEvent>(
    int priority,
    Action<TEvent> callback) : IHostApplicationEventSubscriber<TEvent>, IHostApplicationEventSubscriberPriority
    where TEvent : IHostEvent
{
    public int Priority { get; } = priority;

    public Task HandleAsync(TEvent appEvent, CancellationToken cancellationToken = default)
    {
        callback(appEvent);
        return Task.CompletedTask;
    }
}
