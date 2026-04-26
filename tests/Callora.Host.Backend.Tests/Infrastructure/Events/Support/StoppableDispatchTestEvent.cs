using Callora.Host.Backend.Application.Abstractions.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events.Support;

internal sealed class StoppableDispatchTestEvent : IHostApplicationEvent, IHostApplicationEventPropagationState
{
    public StoppableDispatchTestEvent(DateTimeOffset occurredAtUtc)
    {
        OccurredAtUtc = occurredAtUtc;
    }

    public DateTimeOffset OccurredAtUtc { get; }

    public List<string> Calls { get; } = [];

    public bool IsPropagationStopped { get; private set; }

    public void StopPropagation() => IsPropagationStopped = true;
}
