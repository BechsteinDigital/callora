using Callora.Core.Application.Events.Business;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Application.Events;

/// <summary>
/// The bus used to fan out to every plugin listener regardless of entitlement. That is
/// worse than it sounds for mutable events: <see cref="Callora.Core.Application.Events.MutableBusinessEvent"/>
/// exists so a listener can <b>veto</b> a host operation, so a plugin the workspace no
/// longer holds kept the power to block business operations — invisibly to the customer,
/// whose operation simply failed.
/// </summary>
public sealed class NoEventReachesAnUnavailablePluginTests
{
    [Fact]
    public async Task An_unavailable_plugins_listener_is_not_invoked()
    {
        var appEvent = new TestCreatingBusinessEvent("workspace-a");
        var bus = Bus(
            pluginListener: new MutableBusinessCallbackListener(0, e => e.Calls.Add("plugin")),
            unavailable: "billed-plugin");

        await bus.PublishAsync(appEvent);

        Assert.Empty(appEvent.Calls);
    }

    [Fact]
    public async Task An_unavailable_plugin_cannot_veto_the_operation()
    {
        var appEvent = new TestCreatingBusinessEvent("workspace-a");
        var bus = Bus(
            pluginListener: new MutableBusinessCallbackListener(100, e => e.Cancel()),
            unavailable: "billed-plugin");

        await bus.PublishAsync(appEvent);

        Assert.False(appEvent.IsCanceled);
    }

    [Fact]
    public async Task An_available_plugins_listener_still_runs_and_may_veto()
    {
        var appEvent = new TestCreatingBusinessEvent("workspace-a");
        var bus = Bus(
            pluginListener: new MutableBusinessCallbackListener(100, e => { e.Calls.Add("plugin"); e.Cancel(); }),
            unavailable: "some-other-plugin");

        await bus.PublishAsync(appEvent);

        Assert.Equal(["plugin"], appEvent.Calls);
        Assert.True(appEvent.IsCanceled);
    }

    [Fact]
    public async Task A_host_listener_is_never_gated()
    {
        var appEvent = new TestCreatingBusinessEvent("workspace-a");
        var services = new ServiceCollection();
        services.AddSingleton<IBusinessEventListener>(
            new MutableBusinessCallbackListener(0, e => e.Calls.Add("host")));
        services.AddScoped<IPluginAvailabilityEvaluator>(
            _ => new StaticPluginAvailabilityEvaluator("billed-plugin"));
        await using var provider = services.BuildServiceProvider();

        var bus = new BusinessEventBus(
            provider,
            new StaticPluginCatalog([], pluginId: "billed-plugin"),
            NullLogger<BusinessEventBus>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());

        await bus.PublishAsync(appEvent);

        Assert.Equal(["host"], appEvent.Calls);
    }

    [Fact]
    public async Task A_platform_wide_event_is_judged_on_the_platform_verdict()
    {
        // This pinned a gap until the platform verdict existed: an event naming no workspace
        // was said to ask a question the derivation could not answer. It can — the four
        // host-wide factors are exactly the ones that must hold everywhere.
        var appEvent = new TestCreatingBusinessEvent(null);
        var bus = Bus(
            pluginListener: new MutableBusinessCallbackListener(0, e => e.Calls.Add("plugin")),
            unavailable: "billed-plugin");

        await bus.PublishAsync(appEvent);

        Assert.Empty(appEvent.Calls);
    }

    [Fact]
    public async Task A_platform_wide_event_still_reaches_an_available_plugin()
    {
        var appEvent = new TestCreatingBusinessEvent(null);
        var bus = Bus(
            pluginListener: new MutableBusinessCallbackListener(0, e => e.Calls.Add("plugin")),
            unavailable: "some-other-plugin");

        await bus.PublishAsync(appEvent);

        Assert.Equal(["plugin"], appEvent.Calls);
    }

    /// <remarks>
    /// The evaluator is resolved through a scope factory rather than handed in directly,
    /// because that is how the host does it — the bus is a singleton and the evaluator is
    /// scoped. Wiring the test the same way means this suite also covers the resolution.
    /// </remarks>
    private static BusinessEventBus Bus(IBusinessEventListener pluginListener, string unavailable)
    {
        var catalog = new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>> { [typeof(IBusinessEventListener)] = [pluginListener] },
            pluginId: "billed-plugin");
        var services = new ServiceCollection();
        services.AddScoped<IPluginAvailabilityEvaluator>(
            _ => new StaticPluginAvailabilityEvaluator(unavailable));
        var provider = services.BuildServiceProvider();
        return new BusinessEventBus(
            provider,
            catalog,
            NullLogger<BusinessEventBus>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>());
    }
}

/// <summary>
/// The same gate on the generic host-event dispatcher. It carries the owning plugin id
/// already — for fault attribution — so the only thing missing was asking whether that
/// plugin is still entitled before handing it an event it may veto.
/// </summary>
public sealed class NoHostEventReachesAnUnavailablePluginTests
{
    [Fact]
    public async Task An_unavailable_plugins_subscriber_is_not_invoked()
    {
        var calls = new List<string>();
        var dispatcher = Dispatcher(new RecordingHostEventSubscriber(calls), unavailable: "billed-plugin");

        await dispatcher.DispatchAsync(new WorkspaceScopedHostEvent("workspace-a"));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task An_available_plugins_subscriber_still_runs()
    {
        var calls = new List<string>();
        var dispatcher = Dispatcher(new RecordingHostEventSubscriber(calls), unavailable: "some-other-plugin");

        await dispatcher.DispatchAsync(new WorkspaceScopedHostEvent("workspace-a"));

        Assert.Equal(["plugin"], calls);
    }

    [Fact]
    public async Task A_host_event_without_a_workspace_is_judged_on_the_platform_verdict()
    {
        var calls = new List<string>();
        var dispatcher = Dispatcher(new RecordingHostEventSubscriber(calls), unavailable: "billed-plugin");

        await dispatcher.DispatchAsync(new WorkspaceScopedHostEvent(null));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task A_host_event_without_a_workspace_still_reaches_an_available_plugin()
    {
        var calls = new List<string>();
        var dispatcher = Dispatcher(new RecordingHostEventSubscriber(calls), unavailable: "some-other-plugin");

        await dispatcher.DispatchAsync(new WorkspaceScopedHostEvent(null));

        Assert.Equal(["plugin"], calls);
    }

    private static Callora.Core.Infrastructure.Events.HostApplicationEventDispatcher Dispatcher(
        IHostEventSubscriber<WorkspaceScopedHostEvent> subscriber,
        string unavailable)
    {
        var catalog = new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>>
            {
                [typeof(IHostEventSubscriber<WorkspaceScopedHostEvent>)] = [subscriber]
            },
            pluginId: "billed-plugin");
        return new Callora.Core.Infrastructure.Events.HostApplicationEventDispatcher(
            new ServiceCollection().BuildServiceProvider(),
            catalog,
            NullLogger<Callora.Core.Infrastructure.Events.HostApplicationEventDispatcher>.Instance,
            faults: null,
            availability: new StaticPluginAvailabilityEvaluator(unavailable));
    }
}

internal sealed class WorkspaceScopedHostEvent(string? workspaceKey) : IBusinessEvent
{
    public string EventName => "thing.happening";

    public string? WorkspaceKey { get; } = workspaceKey;

    public IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>();
}

internal sealed class RecordingHostEventSubscriber(List<string> calls)
    : IHostEventSubscriber<WorkspaceScopedHostEvent>
{
    public int Priority => 0;

    public Task HandleAsync(WorkspaceScopedHostEvent appEvent, CancellationToken cancellationToken = default)
    {
        calls.Add("plugin");
        return Task.CompletedTask;
    }
}
