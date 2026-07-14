using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication;
using Callora.Host.Backend.Tests.Support;
using Callora.Plugins.Voip.Application.Calls;

namespace Callora.Host.Backend.Tests.Plugins.Voip;

public sealed class VoipCallHubTests
{
    [Fact]
    public void TrackIncoming_PublishesRingingEvent_AndListsCall()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());
        CallStreamEvent? published = null;
        hub.EventPublished += evt => published = evt;

        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Inbound, CallState.Ringing);
        hub.TrackIncoming("workspace-a", "channel-1", call);

        Assert.Equal(CallEventTypes.Ringing, published!.Type);
        Assert.Single(hub.List("workspace-a"));
    }

    [Fact]
    public void Termination_PublishesEnded_AndRemovesCall()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());
        var events = new List<CallStreamEvent>();
        hub.EventPublished += events.Add;

        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Outbound, CallState.Connected);
        hub.TrackPlaced("workspace-a", "channel-1", call);
        call.TransitionTo(CallState.Terminated);

        Assert.Contains(events, x => x.Type == CallEventTypes.Ended);
        Assert.Empty(hub.List("workspace-a"));
    }

    [Fact]
    public void Termination_DetachesConsentHandler()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());
        var call = new ConsentAwareStaticCall(new CallTarget("+4930111"));
        hub.TrackPlaced("workspace-a", "channel-1", call);
        Assert.Equal(1, call.ConsentSubscriberCount);

        call.TransitionTo(CallState.Terminated);

        // The hub must release its consent handler on termination, otherwise a
        // long-lived call object pins the tracked entry indefinitely (H5).
        Assert.Equal(0, call.ConsentSubscriberCount);
    }

    [Fact]
    public void ConsentChange_AfterTermination_IsNotPublished()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());
        var events = new List<CallStreamEvent>();
        hub.EventPublished += events.Add;

        var call = new ConsentAwareStaticCall(new CallTarget("+4930111"));
        hub.TrackPlaced("workspace-a", "channel-1", call);
        call.TransitionTo(CallState.Terminated);
        events.Clear();

        call.RaiseConsent(RecordingConsentState.Granted);

        Assert.Empty(events);
    }

    [Fact]
    public void StateChange_PublishesStateChangedEvent()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());
        var events = new List<CallStreamEvent>();
        hub.EventPublished += events.Add;

        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Inbound, CallState.Ringing);
        hub.TrackIncoming("workspace-a", "channel-1", call);
        call.TransitionTo(CallState.Connected);

        Assert.Contains(events, x => x.Type == CallEventTypes.StateChanged);
    }

    [Fact]
    public void List_IsScopedToWorkspace()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());
        hub.TrackIncoming("workspace-a", "channel-1",
            new StaticCall(new CallTarget("+1"), CallDirection.Inbound, CallState.Ringing));
        hub.TrackIncoming("workspace-b", "channel-2",
            new StaticCall(new CallTarget("+2"), CallDirection.Inbound, CallState.Ringing));

        Assert.Single(hub.List("workspace-a"));
        Assert.Empty(hub.List("unknown"));
    }

    [Fact]
    public async Task PlaceCall_WithoutChannelId_UsesFirstVoiceChannel_AndTracks()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("voice-1");
        registry.Register("workspace-a", channel);
        var hub = new VoipCallHub(registry);

        var summary = await hub.PlaceCallAsync("workspace-a", channelId: null, new CallTarget("+4930111"));

        Assert.Single(channel.PlacedCalls);
        Assert.Equal("voice-1", summary.ChannelId);
        Assert.Equal("Outbound", summary.Direction);
        Assert.Single(hub.List("workspace-a"));
    }

    [Fact]
    public async Task PlaceCall_WithoutVoiceChannel_Throws()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hub.PlaceCallAsync("workspace-a", channelId: null, new CallTarget("+4930111")));
    }

    [Fact]
    public void AttachToChannels_TracksInboundCallsFromRegisteredChannels()
    {
        var registry = new CommunicationChannelRegistry();
        var hub = new VoipCallHub(registry);
        hub.AttachToChannels();

        var channel = new StaticCommunicationChannel("voice-1");
        registry.Register("workspace-a", channel);
        channel.SimulateIncomingCall(new CallTarget("+4930111"));

        Assert.Single(hub.List("workspace-a"));
    }

    [Fact]
    public async Task Shutdown_HangsUpActiveCalls_AndCompletesStreams()
    {
        var hub = new VoipCallHub(new CommunicationChannelRegistry());
        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Outbound, CallState.Connected);
        hub.TrackPlaced("workspace-a", "voice-1", call);
        using var subscription = hub.Subscribe("workspace-a");

        await hub.ShutdownAsync(CancellationToken.None);

        Assert.Equal(CallState.Terminated, call.State);
        Assert.Empty(hub.List("workspace-a"));

        var remaining = new List<CallStreamEvent>();
        await foreach (var evt in subscription.Reader.ReadAllAsync())
        {
            remaining.Add(evt);
        }

        Assert.Contains(remaining, x => x.Type == CallEventTypes.Ended);
    }
}
