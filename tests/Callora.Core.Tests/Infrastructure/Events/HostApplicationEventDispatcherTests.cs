using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Infrastructure.Events;
using Callora.Core.Tests.Infrastructure.Events.Support;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Events;

public sealed class HostApplicationEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_ExecutesSubscribersInDescendingPriorityOrder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationEventSubscriber<OrderedDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<OrderedDispatchTestEvent>(
                priority: 0,
                callback: appEvent => appEvent.Calls.Add("default")));
        services.AddSingleton<IHostApplicationEventSubscriber<OrderedDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<OrderedDispatchTestEvent>(
                priority: 200,
                callback: appEvent => appEvent.Calls.Add("high")));
        services.AddSingleton<IHostApplicationEventSubscriber<OrderedDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<OrderedDispatchTestEvent>(
                priority: -100,
                callback: appEvent => appEvent.Calls.Add("low")));
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()));

        await using var provider = services.BuildServiceProvider();
        var dispatcher = new HostApplicationEventDispatcher(
            provider,
            provider.GetRequiredService<ICalloraPluginCatalog>(),
            NullLogger<HostApplicationEventDispatcher>.Instance);
        var appEvent = new OrderedDispatchTestEvent(DateTimeOffset.UtcNow);

        await dispatcher.DispatchAsync(appEvent);

        Assert.Equal(["high", "default", "low"], appEvent.Calls);
    }

    [Fact]
    public async Task DispatchAsync_StopsWhenEventPropagationIsStopped()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationEventSubscriber<StoppableDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<StoppableDispatchTestEvent>(
                priority: 100,
                callback: appEvent =>
                {
                    appEvent.Calls.Add("stopper");
                    appEvent.StopPropagation();
                }));
        services.AddSingleton<IHostApplicationEventSubscriber<StoppableDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<StoppableDispatchTestEvent>(
                priority: 0,
                callback: appEvent => appEvent.Calls.Add("should-not-run")));
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()));

        await using var provider = services.BuildServiceProvider();
        var dispatcher = new HostApplicationEventDispatcher(
            provider,
            provider.GetRequiredService<ICalloraPluginCatalog>(),
            NullLogger<HostApplicationEventDispatcher>.Instance);
        var appEvent = new StoppableDispatchTestEvent(DateTimeOffset.UtcNow);

        await dispatcher.DispatchAsync(appEvent);

        Assert.Equal(["stopper"], appEvent.Calls);
    }

    [Fact]
    public async Task DispatchAsync_ExecutesPluginExportSubscribersInSharedPriorityOrder()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationEventSubscriber<OrderedDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<OrderedDispatchTestEvent>(
                priority: 50,
                callback: appEvent => appEvent.Calls.Add("host")));

        var pluginSubscribers = new List<object>
        {
            new CallbackPluginEventSubscriber<OrderedDispatchTestEvent>(
                priority: 200,
                callback: appEvent => appEvent.Calls.Add("plugin-high")),
            new CallbackPluginEventSubscriber<OrderedDispatchTestEvent>(
                priority: -10,
                callback: appEvent => appEvent.Calls.Add("plugin-low")),
        };
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
            {
                [typeof(IHostEventSubscriber<OrderedDispatchTestEvent>)] = pluginSubscribers
            }));

        await using var provider = services.BuildServiceProvider();
        var dispatcher = new HostApplicationEventDispatcher(
            provider,
            provider.GetRequiredService<ICalloraPluginCatalog>(),
            NullLogger<HostApplicationEventDispatcher>.Instance);
        var appEvent = new OrderedDispatchTestEvent(DateTimeOffset.UtcNow);

        await dispatcher.DispatchAsync(appEvent);

        Assert.Equal(["plugin-high", "host", "plugin-low"], appEvent.Calls);
    }

    [Fact]
    public async Task DispatchAsync_WhenAPluginSubscriberThrows_TheFaultIsAttributedToThatPlugin()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(
                new Dictionary<Type, IReadOnlyList<object>>
                {
                    [typeof(IHostEventSubscriber<OrderedDispatchTestEvent>)] =
                    [
                        new CallbackPluginEventSubscriber<OrderedDispatchTestEvent>(
                            priority: 0,
                            callback: _ => throw new InvalidOperationException("boom")),
                    ]
                },
                pluginId: "comm"));

        await using var provider = services.BuildServiceProvider();
        var faults = new PluginFaultRegistry(
            threshold: 1, window: TimeSpan.FromMinutes(5), timeProvider: TimeProvider.System);
        var exceeded = new List<PluginFaultBudgetExceeded>();
        faults.BudgetExceeded += report => exceeded.Add(report);

        var dispatcher = new HostApplicationEventDispatcher(
            provider,
            provider.GetRequiredService<ICalloraPluginCatalog>(),
            NullLogger<HostApplicationEventDispatcher>.Instance,
            faults);

        await dispatcher.DispatchAsync(new OrderedDispatchTestEvent(DateTimeOffset.UtcNow));

        // Der Dispatcher schluckt den Fehler weiterhin — ein Abonnent darf die übrigen nicht
        // mitreißen. Neu ist, dass er dem Verursacher angeschrieben wird, statt nur im Log zu
        // stehen.
        var report = Assert.Single(exceeded);
        Assert.Equal("comm", report.PluginId);
        Assert.Contains(PluginFaultOrigin.Event, report.Origins);
    }

    [Fact]
    public async Task DispatchAsync_WhenAHostSubscriberThrows_NothingIsAttributed()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationEventSubscriber<OrderedDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<OrderedDispatchTestEvent>(
                priority: 0,
                callback: _ => throw new InvalidOperationException("boom")));
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()));

        await using var provider = services.BuildServiceProvider();
        var faults = new PluginFaultRegistry(
            threshold: 1, window: TimeSpan.FromMinutes(5), timeProvider: TimeProvider.System);
        var exceeded = new List<PluginFaultBudgetExceeded>();
        faults.BudgetExceeded += report => exceeded.Add(report);

        var dispatcher = new HostApplicationEventDispatcher(
            provider,
            provider.GetRequiredService<ICalloraPluginCatalog>(),
            NullLogger<HostApplicationEventDispatcher>.Instance,
            faults);

        await dispatcher.DispatchAsync(new OrderedDispatchTestEvent(DateTimeOffset.UtcNow));

        // Ein Host-Abonnent gehört keinem Plugin. Ihn zuzurechnen hieße, irgendein Plugin für
        // einen Fehler des Hosts büßen zu lassen.
        Assert.Empty(exceeded);
    }

    [Fact]
    public async Task DispatchAsync_WhenOneSubscriberThrows_ContinuesRemainingSubscribers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostApplicationEventSubscriber<OrderedDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<OrderedDispatchTestEvent>(
                priority: 100,
                callback: _ => throw new InvalidOperationException("boom")));
        services.AddSingleton<IHostApplicationEventSubscriber<OrderedDispatchTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<OrderedDispatchTestEvent>(
                priority: 0,
                callback: appEvent => appEvent.Calls.Add("after-error")));
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()));

        await using var provider = services.BuildServiceProvider();
        var dispatcher = new HostApplicationEventDispatcher(
            provider,
            provider.GetRequiredService<ICalloraPluginCatalog>(),
            NullLogger<HostApplicationEventDispatcher>.Instance);
        var appEvent = new OrderedDispatchTestEvent(DateTimeOffset.UtcNow);

        await dispatcher.DispatchAsync(appEvent);

        Assert.Equal(["after-error"], appEvent.Calls);
    }
}
