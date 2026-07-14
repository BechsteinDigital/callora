using Callora.Host.Backend.Application.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events.Support;

internal sealed class OrderedDispatchTestEvent : IHostApplicationEvent
{
    public OrderedDispatchTestEvent(DateTimeOffset occurredAtUtc)
    {
        OccurredAtUtc = occurredAtUtc;
    }

    public DateTimeOffset OccurredAtUtc { get; }

    public List<string> Calls { get; } = [];
}
