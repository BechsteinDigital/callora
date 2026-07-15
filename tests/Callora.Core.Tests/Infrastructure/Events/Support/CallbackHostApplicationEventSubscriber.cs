using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Tests.Infrastructure.Events.Support;

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
