using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication;
using Callora.Host.Backend.Application.Communication.Calls;
using Callora.Host.Backend.Tests.Support;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Communication;

public sealed class IncomingCallMonitorTests
{
    [Fact]
    public async Task IncomingCall_OnChannelRegisteredAfterStart_IsTracked()
    {
        var channelRegistry = new CommunicationChannelRegistry();
        var callRegistry = new ActiveCallRegistry(new CallEventBroadcaster());
        var monitor = new IncomingCallMonitor(channelRegistry, callRegistry);
        await monitor.StartAsync(CancellationToken.None);

        var channel = new StaticCommunicationChannel("fake-voice");
        channelRegistry.Register("workspace-a", channel);
        channel.SimulateIncomingCall(new CallTarget("+4930111"));

        var tracked = Assert.Single(callRegistry.List("workspace-a"));
        Assert.Equal("Ringing", tracked.State);
        Assert.Equal("fake-voice", tracked.ChannelId);
    }

    [Fact]
    public async Task IncomingCall_OnChannelRegisteredBeforeStart_IsTracked()
    {
        var channelRegistry = new CommunicationChannelRegistry();
        var callRegistry = new ActiveCallRegistry(new CallEventBroadcaster());
        var channel = new StaticCommunicationChannel("fake-voice");
        channelRegistry.Register("workspace-a", channel);

        var monitor = new IncomingCallMonitor(channelRegistry, callRegistry);
        await monitor.StartAsync(CancellationToken.None);
        channel.SimulateIncomingCall(new CallTarget("+4930111"));

        Assert.Single(callRegistry.List("workspace-a"));
    }

    [Fact]
    public async Task UnregisteredChannel_NoLongerTracksIncomingCalls()
    {
        var channelRegistry = new CommunicationChannelRegistry();
        var callRegistry = new ActiveCallRegistry(new CallEventBroadcaster());
        var monitor = new IncomingCallMonitor(channelRegistry, callRegistry);
        await monitor.StartAsync(CancellationToken.None);

        var channel = new StaticCommunicationChannel("fake-voice");
        var registration = channelRegistry.Register("workspace-a", channel);
        registration.Dispose();
        channel.SimulateIncomingCall(new CallTarget("+4930111"));

        Assert.Empty(callRegistry.List("workspace-a"));
    }

    [Fact]
    public async Task StoppedMonitor_NoLongerTracksIncomingCalls()
    {
        var channelRegistry = new CommunicationChannelRegistry();
        var callRegistry = new ActiveCallRegistry(new CallEventBroadcaster());
        var monitor = new IncomingCallMonitor(channelRegistry, callRegistry);
        await monitor.StartAsync(CancellationToken.None);
        var channel = new StaticCommunicationChannel("fake-voice");
        channelRegistry.Register("workspace-a", channel);

        await monitor.StopAsync(CancellationToken.None);
        channel.SimulateIncomingCall(new CallTarget("+4930111"));

        Assert.Empty(callRegistry.List("workspace-a"));
    }
}
