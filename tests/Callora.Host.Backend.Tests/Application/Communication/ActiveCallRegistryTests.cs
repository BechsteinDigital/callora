using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication.Calls;
using Callora.Host.Backend.Tests.Support;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Communication;

public sealed class ActiveCallRegistryTests
{
    [Fact]
    public void TrackIncoming_ListsCall_AndPublishesRingingEvent()
    {
        var broadcaster = new CallEventBroadcaster();
        var registry = new ActiveCallRegistry(broadcaster);
        using var subscription = broadcaster.Subscribe("workspace-a");
        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Inbound, CallState.Ringing);

        registry.TrackIncoming("workspace-a", "channel-1", call);

        var listed = Assert.Single(registry.List("workspace-a"));
        Assert.Equal(call.CallId, listed.CallId);
        Assert.Equal("Ringing", listed.State);
        Assert.Equal("Inbound", listed.Direction);
        Assert.True(subscription.Reader.TryRead(out var callEvent));
        Assert.Equal(CallEventTypes.Ringing, callEvent!.Type);
    }

    [Fact]
    public void TerminatedCall_IsRemoved_AndPublishesEndedEvent()
    {
        var broadcaster = new CallEventBroadcaster();
        var registry = new ActiveCallRegistry(broadcaster);
        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Inbound, CallState.Ringing);
        registry.TrackIncoming("workspace-a", "channel-1", call);
        using var subscription = broadcaster.Subscribe("workspace-a");

        call.TransitionTo(CallState.Terminated);

        Assert.Empty(registry.List("workspace-a"));
        Assert.True(subscription.Reader.TryRead(out var callEvent));
        Assert.Equal(CallEventTypes.Ended, callEvent!.Type);
    }

    [Fact]
    public void StateChange_PublishesStateChangedEvent()
    {
        var broadcaster = new CallEventBroadcaster();
        var registry = new ActiveCallRegistry(broadcaster);
        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Inbound, CallState.Ringing);
        registry.TrackIncoming("workspace-a", "channel-1", call);
        using var subscription = broadcaster.Subscribe("workspace-a");

        call.TransitionTo(CallState.Connected);

        Assert.True(subscription.Reader.TryRead(out var callEvent));
        Assert.Equal(CallEventTypes.StateChanged, callEvent!.Type);
        Assert.Equal("Connected", callEvent.Call.State);
    }

    [Fact]
    public void TryGet_IsWorkspaceScoped()
    {
        var registry = new ActiveCallRegistry(new CallEventBroadcaster());
        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Inbound, CallState.Ringing);
        registry.TrackIncoming("workspace-a", "channel-1", call);

        Assert.True(registry.TryGet("workspace-a", call.CallId, out _));
        Assert.False(registry.TryGet("workspace-b", call.CallId, out _));
    }

    [Fact]
    public void Events_AreScopedToSubscribedWorkspace()
    {
        var broadcaster = new CallEventBroadcaster();
        var registry = new ActiveCallRegistry(broadcaster);
        using var foreignSubscription = broadcaster.Subscribe("workspace-b");

        registry.TrackIncoming(
            "workspace-a",
            "channel-1",
            new StaticCall(new CallTarget("+4930111"), CallDirection.Inbound, CallState.Ringing));

        Assert.False(foreignSubscription.Reader.TryRead(out _));
    }
}
