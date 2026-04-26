using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Infrastructure.Events;
using Callora.Host.Backend.Tests.Infrastructure.Events.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Infrastructure.Events;

public sealed class HostApplicationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_ExecutesDecoratorsByDescendingDecorationPriority()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationEventDispatcher, RecordingPublishPipelineDispatcher>();
        services.AddSingleton<IHostApplicationEventPublisherDecorator>(_ =>
            new RecordingPublishDecorator("low", decorationPriority: 0));
        services.AddSingleton<IHostApplicationEventPublisherDecorator>(_ =>
            new RecordingPublishDecorator("high", decorationPriority: 200));
        services.AddSingleton<IHostApplicationEventPublisher, HostApplicationEventPublisher>();

        await using var provider = services.BuildServiceProvider();
        var publisher = provider.GetRequiredService<IHostApplicationEventPublisher>();
        var appEvent = new PublishPipelineTestEvent(DateTimeOffset.UtcNow);

        await publisher.PublishAsync(appEvent);

        Assert.Equal(
            ["high.before", "low.before", "dispatch", "low.after", "high.after"],
            appEvent.Steps);
    }
}
