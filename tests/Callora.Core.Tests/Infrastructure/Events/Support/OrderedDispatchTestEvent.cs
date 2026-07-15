using Callora.Core.Application.Events;

namespace Callora.Core.Tests.Infrastructure.Events.Support;

internal sealed class OrderedDispatchTestEvent : IHostApplicationEvent
{
    public OrderedDispatchTestEvent(DateTimeOffset occurredAtUtc)
    {
        OccurredAtUtc = occurredAtUtc;
    }

    public DateTimeOffset OccurredAtUtc { get; }

    public List<string> Calls { get; } = [];
}
