using Callora.Core.Application.Events.Business;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Tests.Support;
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

    [Fact]
    public async Task Publish_NullEvent_ThrowsArgumentNullException()
    {
        var bus = new BusinessEventBus(
            new ServiceCollection().BuildServiceProvider(),
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()),
            NullLogger<BusinessEventBus>.Instance);

        await Assert.ThrowsAsync<ArgumentNullException>(() => bus.PublishAsync(null!));
    }

    [Fact]
    public async Task Publish_CancelledToken_ThrowsAndInvokesNoListener()
    {
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IBusinessEventListener>(new RecordingListener("host", 0, order));
        using var provider = services.BuildServiceProvider();
        var bus = new BusinessEventBus(
            provider,
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()),
            NullLogger<BusinessEventBus>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => bus.PublishAsync(new FakeBusinessEvent("thing.happened", null), cts.Token));
        Assert.Empty(order);
    }

    [Fact]
    public async Task Publish_SameListenerViaDiAndCatalog_InvokedTwice()
    {
        // Characterization: no dedup today — a listener present in both host DI and
        // the plugin catalog fires once per source.
        var order = new List<string>();
        var listener = new RecordingListener("dup", 0, order);
        var services = new ServiceCollection();
        services.AddSingleton<IBusinessEventListener>(listener);
        using var provider = services.BuildServiceProvider();
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IBusinessEventListener)] = [listener]
        });
        var bus = new BusinessEventBus(provider, catalog, NullLogger<BusinessEventBus>.Instance);

        await bus.PublishAsync(new FakeBusinessEvent("thing.happened", null));

        Assert.Equal(["dup", "dup"], order);
    }

    [Fact]
    public async Task Publish_EqualPriority_KeepsHostBeforePluginInRegistrationOrder()
    {
        // Characterization: equal priorities preserve input order (stable sort over the
        // host-DI-then-plugin concat) — host listeners first, in registration order.
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IBusinessEventListener>(new RecordingListener("host-1", 5, order));
        services.AddSingleton<IBusinessEventListener>(new RecordingListener("host-2", 5, order));
        using var provider = services.BuildServiceProvider();
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IBusinessEventListener)] = [new RecordingListener("plugin-1", 5, order)]
        });
        var bus = new BusinessEventBus(provider, catalog, NullLogger<BusinessEventBus>.Instance);

        await bus.PublishAsync(new FakeBusinessEvent("thing.happened", null));

        Assert.Equal(["host-1", "host-2", "plugin-1"], order);
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
