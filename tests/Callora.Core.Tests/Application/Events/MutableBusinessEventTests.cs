using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Business;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Application.Events;

/// <summary>
/// Locks the mutable/cancelable business-event contract (A): a listener can stop the bus
/// fan-out, veto the operation (the publisher sees it while later listeners still run), and
/// share state through the event. Read-only business events are unaffected (covered by
/// <see cref="BusinessEventBusTests"/>).
/// </summary>
public sealed class MutableBusinessEventTests
{
    private static ServiceCollection WithCatalog(ServiceCollection services)
    {
        services.AddSingleton<ICalloraPluginCatalog>(
            new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()));
        return services;
    }

    private static BusinessEventBus Bus(ServiceProvider provider)
        => new(provider, provider.GetRequiredService<ICalloraPluginCatalog>(), NullLogger<BusinessEventBus>.Instance);

    [Fact]
    public async Task Listener_can_stop_further_listeners()
    {
        var services = WithCatalog(new ServiceCollection());
        services.AddSingleton<IBusinessEventListener>(
            new MutableBusinessCallbackListener(100, e => { e.Calls.Add("first"); e.StopPropagation(); }));
        services.AddSingleton<IBusinessEventListener>(
            new MutableBusinessCallbackListener(0, e => e.Calls.Add("second")));

        await using var provider = services.BuildServiceProvider();
        var appEvent = new TestCreatingBusinessEvent("ws");

        await Bus(provider).PublishAsync(appEvent);

        Assert.Equal(["first"], appEvent.Calls);
        Assert.True(appEvent.IsPropagationStopped);
    }

    [Fact]
    public async Task Listener_can_veto_while_later_listeners_still_run()
    {
        var services = WithCatalog(new ServiceCollection());
        services.AddSingleton<IBusinessEventListener>(
            new MutableBusinessCallbackListener(100, e => { e.Calls.Add("veto"); e.Cancel(); }));
        services.AddSingleton<IBusinessEventListener>(
            new MutableBusinessCallbackListener(0, e => e.Calls.Add("after-veto")));

        await using var provider = services.BuildServiceProvider();
        var appEvent = new TestCreatingBusinessEvent("ws");

        await Bus(provider).PublishAsync(appEvent);

        Assert.True(appEvent.IsCanceled);
        Assert.Equal(["veto", "after-veto"], appEvent.Calls);
    }

    [Fact]
    public async Task Listeners_share_state()
    {
        var services = WithCatalog(new ServiceCollection());
        services.AddSingleton<IBusinessEventListener>(
            new MutableBusinessCallbackListener(100, e => e.State["token"] = "abc"));
        services.AddSingleton<IBusinessEventListener>(
            new MutableBusinessCallbackListener(0, e => e.Calls.Add(e.State.TryGetValue("token", out var value) ? value as string ?? "null" : "missing")));

        await using var provider = services.BuildServiceProvider();
        var appEvent = new TestCreatingBusinessEvent("ws");

        await Bus(provider).PublishAsync(appEvent);

        Assert.Equal(["abc"], appEvent.Calls);
    }
}

/// <summary>A concrete mutable business event that records the listeners it reached.</summary>
internal sealed class TestCreatingBusinessEvent(string? workspaceKey)
    : MutableBusinessEvent("workspace.creating", workspaceKey)
{
    public List<string> Calls { get; } = [];

    public override IReadOnlyDictionary<string, string> ToEventData() => new Dictionary<string, string>();
}

internal sealed class MutableBusinessCallbackListener(int priority, Action<TestCreatingBusinessEvent> callback)
    : IBusinessEventListener
{
    public int Priority { get; } = priority;

    public Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default)
    {
        if (businessEvent is TestCreatingBusinessEvent typed)
        {
            callback(typed);
        }

        return Task.CompletedTask;
    }
}
