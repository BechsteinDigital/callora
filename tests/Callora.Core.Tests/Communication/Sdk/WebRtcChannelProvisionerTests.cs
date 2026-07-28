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
        CommunicationChannelRegistry registry) =>
        new(
            client,
            registry,
            "communication",
            NullLogger<WebRtcChannelProvisioner>.Instance);

    [Fact]
    public void GetOrCreateChannel_RegistersChannelInRegistry()
    {
        var client = new FakeWebRtcClient();
        var registry = new CommunicationChannelRegistry();
        var provisioner = NewProvisioner(client, registry);

        provisioner.GetOrCreateChannel("ws-1");

        var channels = registry.GetChannels("ws-1");
        Assert.Single(channels);
        Assert.Contains(CommunicationCapabilities.Voice, channels[0].Capabilities);
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
        Assert.Single(registry.GetChannels("ws-1"));
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
        Assert.Single(registry.GetChannels("ws-a"));
        Assert.Single(registry.GetChannels("ws-b"));
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
}
