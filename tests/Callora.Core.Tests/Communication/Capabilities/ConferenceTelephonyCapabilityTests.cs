using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Capabilities;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Core.Application.Plugins.Contracts;
using Xunit;

namespace Callora.Core.Tests.Communication.Capabilities;

/// <summary>
/// The capability that says a caller can actually be put into a conference. It is derived, not
/// declared: no single channel can claim it, because bridging needs a call to exist and a room to
/// exist at the same time.
/// </summary>
/// <remarks>The conference side is stood in for by a channel publishing the video capability, which is
/// what the real <c>ConferenceChannel</c> publishes.</remarks>
public sealed class ConferenceTelephonyCapabilityTests
{
    private const string Workspace = "ws-1";

    [Fact]
    public void VoiceAlone_DoesNotGrantIt()
    {
        var registry = new CommunicationChannelRegistry();
        var source = new CommunicationRuntimeCapabilitySource(registry, conferenceBridgingAvailable: true);

        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = "voice", Health = ChannelHealth.Up });

        // A phone can call in, but there is no room to put it into.
        Assert.DoesNotContain(Granted(source), g => g.Capability == CommunicationCapabilities.ConferenceTelephony);
    }

    [Fact]
    public void AConferenceAlone_DoesNotGrantIt()
    {
        var registry = new CommunicationChannelRegistry();
        var source = new CommunicationRuntimeCapabilitySource(registry, conferenceBridgingAvailable: true);

        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = "conf", Health = ChannelHealth.Up, Capabilities = [CommunicationCapabilities.Video] });

        // There is a room, but no telephony to reach it from.
        Assert.DoesNotContain(Granted(source), g => g.Capability == CommunicationCapabilities.ConferenceTelephony);
    }

    [Fact]
    public void BothTogether_GrantIt()
    {
        var registry = new CommunicationChannelRegistry();
        var source = new CommunicationRuntimeCapabilitySource(registry, conferenceBridgingAvailable: true);

        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = "voice", Health = ChannelHealth.Up });
        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = "conf", Health = ChannelHealth.Up, Capabilities = [CommunicationCapabilities.Video] });

        Assert.Contains(Granted(source), g => g.Capability == CommunicationCapabilities.ConferenceTelephony);
    }

    [Fact]
    public void WithoutTheBridgingPort_ItIsNotGrantedEvenWithBothChannels()
    {
        var registry = new CommunicationChannelRegistry();
        var source = new CommunicationRuntimeCapabilitySource(registry, conferenceBridgingAvailable: false);

        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = "voice", Health = ChannelHealth.Up });
        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = "conf", Health = ChannelHealth.Up, Capabilities = [CommunicationCapabilities.Video] });

        // The deployment has calls and rooms but no attachment to join them — the capability would be a
        // promise nothing can keep.
        Assert.DoesNotContain(Granted(source), g => g.Capability == CommunicationCapabilities.ConferenceTelephony);
    }

    [Fact]
    public void AnUnhealthyVoiceChannel_WithdrawsIt()
    {
        var registry = new CommunicationChannelRegistry();
        var source = new CommunicationRuntimeCapabilitySource(registry, conferenceBridgingAvailable: true);
        var voice = new FakeVoiceChannel { ChannelId = "voice", Health = ChannelHealth.Up };
        registry.Register(Workspace, voice);
        registry.Register(Workspace, new FakeVoiceChannel { ChannelId = "conf", Health = ChannelHealth.Up, Capabilities = [CommunicationCapabilities.Video] });

        voice.Health = ChannelHealth.Down;
        voice.RaiseHealthChanged(ChannelHealth.Down);

        Assert.DoesNotContain(Granted(source), g => g.Capability == CommunicationCapabilities.ConferenceTelephony);
    }

    private static System.Collections.Generic.IReadOnlyCollection<RuntimeCapabilityGrant> Granted(
        CommunicationRuntimeCapabilitySource source) => source.CurrentGrants;
}
