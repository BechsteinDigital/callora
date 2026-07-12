using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication;
using Callora.Host.Backend.Application.Communication.Calls;
using Callora.Host.Backend.Tests.Support;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Communication;

public sealed class CallPlacementServiceTests
{
    [Fact]
    public async Task PlaceCall_WithoutChannelId_UsesFirstVoiceChannel_AndTracksCall()
    {
        var channelRegistry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("fake-voice");
        channelRegistry.Register("workspace-a", channel);
        var callRegistry = new ActiveCallRegistry(new CallEventBroadcaster());
        var service = new CallPlacementService(channelRegistry, callRegistry);

        var snapshot = await service.PlaceCallAsync("workspace-a", channelId: null, new CallTarget("+4930111"));

        Assert.Single(channel.PlacedCalls);
        Assert.Equal("fake-voice", snapshot.ChannelId);
        Assert.Equal("Outbound", snapshot.Direction);
        Assert.Single(callRegistry.List("workspace-a"));
    }

    [Fact]
    public async Task PlaceCall_WithExplicitChannelId_UsesThatChannel()
    {
        var channelRegistry = new CommunicationChannelRegistry();
        var first = new StaticCommunicationChannel("voice-1");
        var second = new StaticCommunicationChannel("voice-2");
        channelRegistry.Register("workspace-a", first);
        channelRegistry.Register("workspace-a", second);
        var service = new CallPlacementService(channelRegistry, new ActiveCallRegistry(new CallEventBroadcaster()));

        await service.PlaceCallAsync("workspace-a", "voice-2", new CallTarget("+4930111"));

        Assert.Empty(first.PlacedCalls);
        Assert.Single(second.PlacedCalls);
    }

    [Fact]
    public async Task PlaceCall_WithoutVoiceChannel_Throws()
    {
        var service = new CallPlacementService(
            new CommunicationChannelRegistry(),
            new ActiveCallRegistry(new CallEventBroadcaster()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PlaceCallAsync("workspace-a", channelId: null, new CallTarget("+4930111")));
    }

    [Fact]
    public async Task PlaceCall_WithUnknownChannelId_Throws()
    {
        var channelRegistry = new CommunicationChannelRegistry();
        channelRegistry.Register("workspace-a", new StaticCommunicationChannel("voice-1"));
        var service = new CallPlacementService(channelRegistry, new ActiveCallRegistry(new CallEventBroadcaster()));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PlaceCallAsync("workspace-a", "voice-99", new CallTarget("+4930111")));
    }
}
