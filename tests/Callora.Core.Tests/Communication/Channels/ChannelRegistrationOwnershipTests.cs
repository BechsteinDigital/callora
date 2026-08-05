using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Xunit;

namespace Callora.Core.Tests.Communication.Channels;

/// <summary>
/// A registration handle owns exactly the registration it created (#117).
/// <para>
/// Removal used to key on the channel id alone, so after a clear-and-re-register cycle the old
/// handle deregistered the new channel. The provisioner reuses its ids
/// (<c>webrtc-{workspace}</c>, <c>conference-{workspace}</c>), which makes that collision the
/// normal case on plugin restart rather than an edge case.
/// </para>
/// </summary>
public sealed class ChannelRegistrationOwnershipTests
{
    private const string Workspace = "ws-a";
    private const string ChannelId = "voice-1";

    [Fact]
    public void StaleHandle_DoesNotRemoveTheReRegisteredChannel()
    {
        var registry = new CommunicationChannelRegistry();
        var original = new FakeVoiceChannel { ChannelId = ChannelId };
        var staleHandle = registry.Register(Workspace, original);

        registry.Clear();
        var replacement = new FakeVoiceChannel { ChannelId = ChannelId };
        registry.Register(Workspace, replacement);

        staleHandle.Dispose();

        var remaining = Assert.Single(registry.GetChannels(Workspace));
        Assert.Same(replacement, remaining);
    }

    [Fact]
    public void StaleHandle_DoesNotRaiseUnregisteredForTheNewChannel()
    {
        // The capability source listens on this event; a spurious unregister would revoke a
        // capability the live channel still provides.
        var registry = new CommunicationChannelRegistry();
        var staleHandle = registry.Register(Workspace, new FakeVoiceChannel { ChannelId = ChannelId });
        registry.Clear();
        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = ChannelId });

        var unregistered = new List<string>();
        registry.ChannelUnregistered += (_, channel) => unregistered.Add(channel.ChannelId);
        staleHandle.Dispose();

        Assert.Empty(unregistered);
    }

    [Fact]
    public void OwnHandle_StillRemovesItsRegistration()
    {
        var registry = new CommunicationChannelRegistry();
        var handle = registry.Register(Workspace, new FakeVoiceChannel { ChannelId = ChannelId });

        handle.Dispose();

        Assert.Empty(registry.GetChannels(Workspace));
    }

    [Fact]
    public void DisposingAHandleTwice_RemovesOnce()
    {
        var registry = new CommunicationChannelRegistry();
        var handle = registry.Register(Workspace, new FakeVoiceChannel { ChannelId = ChannelId });
        handle.Dispose();

        var replacement = new FakeVoiceChannel { ChannelId = ChannelId };
        registry.Register(Workspace, replacement);
        handle.Dispose();

        Assert.Same(replacement, Assert.Single(registry.GetChannels(Workspace)));
    }

    [Fact]
    public void ReRegisteringWhileTheOriginalIsStillHeld_IsRejected()
    {
        // Two live channels with one id in one workspace would make routing ambiguous.
        var registry = new CommunicationChannelRegistry();
        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = ChannelId });

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(Workspace, new FakeVoiceChannel { ChannelId = ChannelId }));
    }

    [Fact]
    public void SameChannelId_InDifferentWorkspaces_StaysIndependent()
    {
        var registry = new CommunicationChannelRegistry();
        var first = new FakeVoiceChannel { ChannelId = ChannelId };
        var second = new FakeVoiceChannel { ChannelId = ChannelId };
        var firstHandle = registry.Register("ws-1", first);
        registry.Register("ws-2", second);

        firstHandle.Dispose();

        Assert.Empty(registry.GetChannels("ws-1"));
        Assert.Same(second, Assert.Single(registry.GetChannels("ws-2")));
    }
}
