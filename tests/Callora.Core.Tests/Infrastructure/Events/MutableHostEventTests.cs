using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Infrastructure.Events;
using Callora.Core.Tests.Infrastructure.Events.Support;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Events;

/// <summary>
/// Locks the mutable/cancelable host-event contract (A): subscribers can stop further
/// subscribers, veto the operation (the caller sees it while later subscribers still run),
/// and share state through the event.
/// </summary>
public sealed class MutableHostEventTests
{
    private static ServiceCollection WithCatalog(ServiceCollection services)
    {
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()));
        return services;
    }

    private static HostApplicationEventDispatcher Dispatcher(ServiceProvider provider)
        => new(provider, provider.GetRequiredService<ICalloraPluginCatalog>(), NullLogger<HostApplicationEventDispatcher>.Instance);

    [Fact]
    public async Task Subscriber_can_stop_further_subscribers()
    {
        var services = WithCatalog(new ServiceCollection());
        services.AddSingleton<IHostApplicationEventSubscriber<MutableTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<MutableTestEvent>(
                priority: 100,
                callback: e => { e.Calls.Add("first"); e.StopPropagation(); }));
        services.AddSingleton<IHostApplicationEventSubscriber<MutableTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<MutableTestEvent>(
                priority: 0,
                callback: e => e.Calls.Add("second")));

        await using var provider = services.BuildServiceProvider();
        var appEvent = new MutableTestEvent(DateTimeOffset.UtcNow);

        await Dispatcher(provider).DispatchAsync(appEvent);

        Assert.Equal(["first"], appEvent.Calls);
        Assert.True(appEvent.IsPropagationStopped);
    }

    [Fact]
    public async Task Subscriber_can_veto_while_later_subscribers_still_run()
    {
        var services = WithCatalog(new ServiceCollection());
        services.AddSingleton<IHostApplicationEventSubscriber<MutableTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<MutableTestEvent>(
                priority: 100,
                callback: e => { e.Calls.Add("veto"); e.Cancel(); }));
        services.AddSingleton<IHostApplicationEventSubscriber<MutableTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<MutableTestEvent>(
                priority: 0,
                callback: e => e.Calls.Add("after-veto")));

        await using var provider = services.BuildServiceProvider();
        var appEvent = new MutableTestEvent(DateTimeOffset.UtcNow);

        await Dispatcher(provider).DispatchAsync(appEvent);

        // Cancel is a veto the caller inspects afterwards; it does NOT stop propagation.
        Assert.True(appEvent.IsCanceled);
        Assert.Equal(["veto", "after-veto"], appEvent.Calls);
    }

    [Fact]
    public async Task Subscribers_share_state()
    {
        var services = WithCatalog(new ServiceCollection());
        services.AddSingleton<IHostApplicationEventSubscriber<MutableTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<MutableTestEvent>(
                priority: 100,
                callback: e => e.State["token"] = "abc"));
        services.AddSingleton<IHostApplicationEventSubscriber<MutableTestEvent>>(_ =>
            new CallbackHostApplicationEventSubscriber<MutableTestEvent>(
                priority: 0,
                callback: e => e.Calls.Add(e.State.TryGetValue("token", out var value) ? value as string ?? "null" : "missing")));

        await using var provider = services.BuildServiceProvider();
        var appEvent = new MutableTestEvent(DateTimeOffset.UtcNow);

        await Dispatcher(provider).DispatchAsync(appEvent);

        Assert.Equal(["abc"], appEvent.Calls);
    }
}

/// <summary>A concrete mutable event that records the subscribers it reached.</summary>
internal sealed class MutableTestEvent(DateTimeOffset occurredAtUtc) : MutableHostEvent(occurredAtUtc)
{
    public List<string> Calls { get; } = [];
}
