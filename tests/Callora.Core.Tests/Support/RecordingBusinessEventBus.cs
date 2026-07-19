using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>Test double that records the business events published to it.</summary>
public sealed class RecordingBusinessEventBus : IBusinessEventBus
{
    public List<IBusinessEvent> Published { get; } = [];

    public Task PublishAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default)
    {
        Published.Add(businessEvent);
        return Task.CompletedTask;
    }
}
