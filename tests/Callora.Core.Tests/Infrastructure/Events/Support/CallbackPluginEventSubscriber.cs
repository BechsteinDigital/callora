using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Tests.Infrastructure.Events.Support;

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
