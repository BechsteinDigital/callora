using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Channels;
using Xunit;

namespace Callora.Core.Tests.Application.Communication;

public sealed class CommunicationChannelRegistryTests
{
    [Fact]
    public void Register_MakesChannelResolvableForWorkspace()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("trunk-1");

        registry.Register("workspace-a", channel);

        var channels = registry.GetChannels("workspace-a");
        Assert.Single(channels);
        Assert.Same(channel, channels[0]);
    }

    [Fact]
    public void Register_DuplicateChannelIdInSameWorkspace_Throws()
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("workspace-a", new StaticCommunicationChannel("trunk-1"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("workspace-a", new StaticCommunicationChannel("TRUNK-1")));
    }

    [Fact]
    public void Register_SameChannelIdInDifferentWorkspaces_IsIsolated()
    {
        var registry = new CommunicationChannelRegistry();
        var channelA = new StaticCommunicationChannel("trunk-1");
        var channelB = new StaticCommunicationChannel("trunk-1");

        registry.Register("workspace-a", channelA);
        registry.Register("workspace-b", channelB);

        Assert.Same(channelA, Assert.Single(registry.GetChannels("workspace-a")));
        Assert.Same(channelB, Assert.Single(registry.GetChannels("workspace-b")));
    }

    [Fact]
    public void DisposeRegistration_RemovesChannel_AndIsIdempotent()
    {
        var registry = new CommunicationChannelRegistry();
        var registration = registry.Register("workspace-a", new StaticCommunicationChannel("trunk-1"));

        registration.Dispose();
        registration.Dispose();

        Assert.Empty(registry.GetChannels("workspace-a"));
    }

    [Fact]
    public void GetChannelsByCapability_FiltersByCapabilityCode()
    {
        var registry = new CommunicationChannelRegistry();
        var voiceChannel = new StaticCommunicationChannel(
            "trunk-1",
            capabilities: [CommunicationCapabilities.Voice]);
        var otherChannel = new StaticCommunicationChannel(
            "messaging-1",
            capabilities: ["communication.messaging"]);
        registry.Register("workspace-a", voiceChannel);
        registry.Register("workspace-a", otherChannel);

        var voiceChannels = registry.GetChannelsByCapability("workspace-a", CommunicationCapabilities.Voice);

        Assert.Same(voiceChannel, Assert.Single(voiceChannels));
    }

    [Fact]
    public void TryGetChannel_FindsChannelCaseInsensitive()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new StaticCommunicationChannel("Trunk-1");
        registry.Register("workspace-a", channel);

        var found = registry.TryGetChannel("workspace-a", "trunk-1", out var resolved);

        Assert.True(found);
        Assert.Same(channel, resolved);
    }

    [Fact]
    public void GetChannels_UnknownWorkspace_ReturnsEmpty()
    {
        var registry = new CommunicationChannelRegistry();

        Assert.Empty(registry.GetChannels("unknown"));
        Assert.False(registry.TryGetChannel("unknown", "trunk-1", out var channel));
        Assert.Null(channel);
    }
}
