using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// The call-control operations a dialer and an automation both need (#116). The underlying call
/// abstraction always supported accept, reject and DTMF; the service exposed only place and hang up,
/// so no caller could reach them.
/// </summary>
public sealed class CallControlOperationsTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public async Task ARingingInboundCall_CanBeAnswered()
    {
        var (service, _, call) = await RingingInboundAsync();

        Assert.True(await service.AcceptAsync(Workspace, "call-1"));

        Assert.True(call.AcceptCalled);
        Assert.Equal(CallState.Connected, service.Get(Workspace, "call-1")!.State);
    }

    [Fact]
    public async Task AnsweringRecordsTheAnswer()
    {
        // The answer has to reach history through the same path a remote answer takes, otherwise a
        // call answered from the dialer would look unanswered in the log.
        var (service, store, _) = await RingingInboundAsync();

        await service.AcceptAsync(Workspace, "call-1");

        Assert.NotNull(store.Added[0].AnsweredAt);
        Assert.Contains(store.OutboxEntries, x => x.EventName == CallEventTypes.StateChanged);
    }

    [Fact]
    public async Task ARingingInboundCall_CanBeRejected()
    {
        var (service, store, call) = await RingingInboundAsync();

        Assert.True(await service.RejectAsync(Workspace, "call-1"));

        Assert.True(call.RejectCalled);
        Assert.NotNull(store.Added[0].EndedAt);
        Assert.Null(store.Added[0].AnsweredAt);
    }

    [Fact]
    public async Task AnOutboundCall_CannotBeAnswered()
    {
        // Reported as a rejected transition, not as a missing call: the call is right there.
        var (service, _, _) = await PlacedOutboundAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync(Workspace, "call-1"));
    }

    [Fact]
    public async Task AnAlreadyConnectedCall_CannotBeAnsweredAgain()
    {
        var (service, _, call) = await RingingInboundAsync();
        call.Transition(CallState.Connected);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AcceptAsync(Workspace, "call-1"));
    }

    [Fact]
    public async Task AnotherWorkspacesCall_CannotBeControlled()
    {
        var (service, _, call) = await RingingInboundAsync();

        Assert.False(await service.AcceptAsync("ws-b", "call-1"));
        Assert.False(await service.RejectAsync("ws-b", "call-1"));
        Assert.False(await service.HangupAsync("ws-b", "call-1"));
        Assert.False(await service.SendDtmfAsync("ws-b", "call-1", "1"));
        Assert.False(call.AcceptCalled);
    }

    [Fact]
    public async Task DtmfTonesReachTheCallInOrder()
    {
        var (service, _, call) = await PlacedOutboundAsync();

        Assert.True(await service.SendDtmfAsync(Workspace, "call-1", "12*#"));

        Assert.Equal(['1', '2', '*', '#'], call.SentTones);
    }

    [Fact]
    public async Task AnInvalidToneSendsNothingAtAll()
    {
        // Rejected whole rather than half-sent, so a caller never has to guess how far it got.
        var (service, _, call) = await PlacedOutboundAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendDtmfAsync(Workspace, "call-1", "12X4"));

        Assert.Empty(call.SentTones);
    }

    [Fact]
    public async Task ListActiveShowsOnlyTheWorkspacesLiveCalls()
    {
        var registry = new CommunicationChannelRegistry();
        await using var service = NewService(registry, new RecordingCallLogStore());
        registry.Register(Workspace, new FakeCommunicationChannel { ChannelId = "ch-1", NextCall = new ControllableCall("call-1") });
        registry.Register("ws-b", new FakeCommunicationChannel { ChannelId = "ch-2", NextCall = new ControllableCall("call-2") });

        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567", ChannelId: "ch-1"));
        await service.PlaceCallAsync(new PlaceCallCommand("ws-b", "+49301234568", ChannelId: "ch-2"));

        Assert.Equal(["call-1"], service.ListActive(Workspace).Select(x => x.CallId));
        Assert.Equal(["call-2"], service.ListActive("ws-b").Select(x => x.CallId));
    }

    [Fact]
    public async Task AnEndedCall_DropsOutOfTheActiveList()
    {
        var (service, _, call) = await PlacedOutboundAsync();
        Assert.Single(service.ListActive(Workspace));

        call.Transition(CallState.Terminated);

        Assert.Empty(service.ListActive(Workspace));
    }

    private static CallControlService NewService(CommunicationChannelRegistry registry, RecordingCallLogStore store) =>
        new(registry, store, NullLogger<CallControlService>.Instance, TimeProvider.System);

    private static async Task<(CallControlService Service, RecordingCallLogStore Store, ControllableCall Call)>
        PlacedOutboundAsync()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var service = NewService(registry, store);
        var call = new ControllableCall("call-1");
        registry.Register(Workspace, new FakeCommunicationChannel { NextCall = call });

        await service.PlaceCallAsync(new PlaceCallCommand(Workspace, "+49301234567"));
        return (service, store, call);
    }

    private static async Task<(CallControlService Service, RecordingCallLogStore Store, ControllableCall Call)>
        RingingInboundAsync()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var service = NewService(registry, store);
        var channel = new FakeCommunicationChannel { ChannelId = "ch-1" };
        var call = new ControllableCall("call-1", CallState.Ringing, CallDirection.Inbound, "+49301234567");
        registry.Register(Workspace, channel);

        await service.ObserveIncomingAsync(Workspace, channel, call);
        return (service, store, call);
    }
}
