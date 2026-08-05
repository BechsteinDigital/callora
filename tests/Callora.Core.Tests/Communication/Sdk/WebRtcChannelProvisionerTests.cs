using System.Linq;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The WebRTC channel provisioner (S4): get-or-create idempotent, workspace-isolated,
/// teardown deregisters channels, client is never disposed by the provisioner.
/// </summary>
public sealed class WebRtcChannelProvisionerTests
{
    private static WebRtcChannelProvisioner NewProvisioner(
        FakeWebRtcClient client,
        CommunicationChannelRegistry registry,
        bool externallyReachable = true) =>
        new(
            client,
            registry,
            "communication",
            externallyReachable,
            NullLogger<WebRtcChannelProvisioner>.Instance);

    [Fact]
    public void GetOrCreateChannel_RegistersChannelInRegistry()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry);

        provisioner.GetOrCreateChannel("ws-1");

        // The WebRTC surface and the conference surface ride the same client and the same NAT
        // traversal, so both are registered together; the conference one is what makes
        // communication.video satisfiable (#115).
        var channels = registry.GetChannels("ws-1");
        Assert.Equal(2, channels.Count);
        Assert.Contains(channels, x => x.Capabilities.Contains(CommunicationCapabilities.Voice));
        Assert.Contains(channels, x => x.Capabilities.Contains(CommunicationCapabilities.WebRtc));
        Assert.Contains(channels, x => x.Capabilities.Contains(CommunicationCapabilities.Video));
    }

    [Fact]
    public void GetOrCreateChannel_SameWorkspace_ReturnsSameInstance_NoDoubleRegistration()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry);

        var first = provisioner.GetOrCreateChannel("ws-1");
        var second = provisioner.GetOrCreateChannel("ws-1");

        Assert.Same(first, second);
        Assert.Equal(2, registry.GetChannels("ws-1").Count);
    }

    [Fact]
    public void GetOrCreateChannel_DifferentWorkspaces_ReturnDifferentChannels()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry);

        var chA = provisioner.GetOrCreateChannel("ws-a");
        var chB = provisioner.GetOrCreateChannel("ws-b");

        Assert.NotSame(chA, chB);
        Assert.Equal(2, registry.GetChannels("ws-a").Count);
        Assert.Equal(2, registry.GetChannels("ws-b").Count);
    }

    [Fact]
    public void Teardown_DeregistersAllChannels()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry);

        provisioner.GetOrCreateChannel("ws-1");
        provisioner.GetOrCreateChannel("ws-2");

        provisioner.Teardown();

        Assert.Empty(registry.GetChannels("ws-1"));
        Assert.Empty(registry.GetChannels("ws-2"));
    }

    [Fact]
    public void Teardown_DoesNotDisposeClient()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry);

        provisioner.GetOrCreateChannel("ws-1");
        provisioner.Teardown();

        Assert.False(client.DisposeAsyncCalled);
    }

    [Fact]
    public void Client_Property_ReturnsInjectedClient()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry);

        Assert.Same(client, provisioner.Client);
    }

    [Fact]
    public void GetOrCreateChannel_ExternallyReachable_ReportsHealthUp()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry, externallyReachable: true);

        var channel = provisioner.GetOrCreateChannel("ws-reach");

        Assert.Equal(Callora.Plugin.Communication.Abstractions.ChannelHealth.Up, channel.Health);
    }

    [Fact]
    public void GetOrCreateChannel_NotExternallyReachable_ReportsHealthDegraded()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry, externallyReachable: false);

        var channel = provisioner.GetOrCreateChannel("ws-noreach");

        Assert.Equal(Callora.Plugin.Communication.Abstractions.ChannelHealth.Degraded, channel.Health);
    }
}
