using Callora.Core.Application.Events.Business;
using Callora.Core.Tests.Support;
using Callora.Core.Application.Events.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Application.Events;

public sealed class BusinessEventBusTests
{
    [Fact]
    public async Task Publish_FansOutToHostAndPluginListeners_InPriorityOrder()
    {
        var order = new List<string>();
        var lowHost = new RecordingListener("host-low", 0, order);
        var highPlugin = new RecordingListener("plugin-high", 100, order);

        var services = new ServiceCollection();
        services.AddSingleton<IBusinessEventListener>(lowHost);
        using var provider = services.BuildServiceProvider();
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IBusinessEventListener)] = [highPlugin]
        });

        var bus = new BusinessEventBus(provider, catalog, NullLogger<BusinessEventBus>.Instance);
        await bus.PublishAsync(new FakeBusinessEvent("thing.happened", "workspace-a"));

        Assert.Equal(["plugin-high", "host-low"], order);
    }

    [Fact]
    public async Task Publish_IsolatesFailingListener()
    {
        var order = new List<string>();
        var throwing = new ThrowingListener();
        var good = new RecordingListener("good", 0, order);

        var services = new ServiceCollection();
        services.AddSingleton<IBusinessEventListener>(throwing);
        services.AddSingleton<IBusinessEventListener>(good);
        using var provider = services.BuildServiceProvider();

        var bus = new BusinessEventBus(
            provider,
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()),
            NullLogger<BusinessEventBus>.Instance);
        await bus.PublishAsync(new FakeBusinessEvent("thing.happened", null));

        Assert.Contains("good", order);
    }

    private sealed class FakeBusinessEvent(string name, string? workspaceKey) : IBusinessEvent
    {
        public string EventName => name;
        public string? WorkspaceKey => workspaceKey;
        public IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>();
    }

    private sealed class RecordingListener(string id, int priority, List<string> order) : IBusinessEventListener
    {
        public int Priority => priority;
        public Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default)
        {
            order.Add(id);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingListener : IBusinessEventListener
    {
        public int Priority => 50;
        public Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("boom");
    }
}
