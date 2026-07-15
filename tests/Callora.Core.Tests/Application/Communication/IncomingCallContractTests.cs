using Callora.Plugin.Communication.Abstractions;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Communication;

/// <summary>
/// Contract proof: inbound calls flow over the channel-neutral contracts —
/// these tests use the protocol-free fake channel, not SIP.
/// </summary>
public sealed class IncomingCallContractTests
{
    [Fact]
    public async Task IncomingCall_CanBeAccepted_OverContractChannel()
    {
        var channel = new StaticCommunicationChannel("fake-voice");
        var receivedCalls = new List<ICall>();
        channel.IncomingCall += (_, args) => receivedCalls.Add(args.Call);

        channel.SimulateIncomingCall(new CallTarget("+4930111", "Caller"));

        var call = Assert.Single(receivedCalls);
        Assert.Equal(CallState.Ringing, call.State);
        Assert.Equal(CallDirection.Inbound, call.Direction);
        Assert.Equal("+4930111", call.Target.Value);

        await call.AcceptAsync();

        Assert.Equal(CallState.Connected, call.State);
    }

    [Fact]
    public async Task IncomingCall_CanBeRejected_OverContractChannel()
    {
        var channel = new StaticCommunicationChannel("fake-voice");
        ICall? received = null;
        channel.IncomingCall += (_, args) => received = args.Call;

        channel.SimulateIncomingCall(new CallTarget("+4930111"));

        await received!.RejectAsync();

        Assert.Equal(CallState.Terminated, received.State);
    }

    [Fact]
    public async Task AcceptingOutboundCall_Throws()
    {
        var channel = new StaticCommunicationChannel("fake-voice");
        var call = await channel.PlaceCallAsync(new CallTarget("+4930111"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.AcceptAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => call.RejectAsync());
    }

    [Fact]
    public async Task AcceptingAlreadyAnsweredInboundCall_Throws()
    {
        var channel = new StaticCommunicationChannel("fake-voice");
        var call = channel.SimulateIncomingCall(new CallTarget("+4930111"));
        await call.AcceptAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.AcceptAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => call.RejectAsync());
    }
}
