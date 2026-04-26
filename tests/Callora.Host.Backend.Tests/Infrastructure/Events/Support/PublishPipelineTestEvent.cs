using Callora.Host.Backend.Application.Abstractions.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events.Support;

internal sealed class PublishPipelineTestEvent : IHostApplicationEvent
{
    public PublishPipelineTestEvent(DateTimeOffset occurredAtUtc)
    {
        OccurredAtUtc = occurredAtUtc;
    }

    public DateTimeOffset OccurredAtUtc { get; }

    public List<string> Steps { get; } = [];
}
