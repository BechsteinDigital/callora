using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Tests.Support;
using Callora.Host.Backend.Infrastructure.Events;
using Callora.Host.Backend.Tests.Infrastructure.Events.Support;
using Callora.Modules.Abstractions.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VoipHost.PluginContracts.Application.Events;

namespace Callora.Host.Backend.Tests.Infrastructure.Events;

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
