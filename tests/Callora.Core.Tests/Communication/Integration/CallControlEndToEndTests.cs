using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Communication.Calls;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Admin.Calls;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Flows;
using Callora.Plugin.Communication.Domain.Calls;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Integration;

/// <summary>
/// A whole call, driven the way a client drives it (#116): the REST routes over the real
/// call-control service and a real channel registry, with the live stream attached.
/// </summary>
/// <remarks>
/// The unit tests prove each piece; these prove they compose — that answering over REST really
/// reaches the call, really records the answer, and really shows up on the stream a dialer follows.
/// </remarks>
public sealed class CallControlEndToEndTests
{
    private const string Workspace = "ws-a";
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnInboundCall_IsAnnounced_Answered_Toned_AndEnded()
    {
        var world = await NewWorldAsync();
        var call = new ControllableCall("call-in", CallState.Ringing, CallDirection.Inbound, "+49301234567");
        await world.Service.ObserveIncomingAsync(Workspace, world.Channel, call);

        // It is announced on the stream and appears in the live list.
        Assert.True(world.Subscription.Events.TryRead(out var ringing));
        Assert.Equal(CallEventTypes.Ringing, ringing!.EventName);
        var listed = Assert.IsType<CallView[]>(
            (await new ListActiveCallsRouteHandler(world.Service).HandleAsync(Get("calls/active"))).Payload);
        Assert.Equal("call-in", Assert.Single(listed).CallId);

        // Answered over REST.
        Assert.Equal(204, (await new AcceptCallRouteHandler(world.Service)
            .HandleAsync(Post("calls/call-in/accept", "call-in"))).StatusCode);
        Assert.True(call.AcceptCalled);
        Assert.NotNull(world.Store.Added[0].AnsweredAt);

        // Tones reach the call while it is connected.
        Assert.Equal(204, (await new SendDtmfRouteHandler(world.Service)
            .HandleAsync(Post("calls/call-in/dtmf", "call-in", new { tones = "42#" }))).StatusCode);
        Assert.Equal(['4', '2', '#'], call.SentTones);

        // Hung up over REST; the history is final and the live list is empty again.
        Assert.Equal(204, (await new HangupCallRouteHandler(world.Service)
            .HandleAsync(Post("calls/call-in/hangup", "call-in"))).StatusCode);
        Assert.Equal(CallOutcome.Completed, world.Store.Added[0].Outcome);
        Assert.Empty(world.Service.ListActive(Workspace));
    }

    [Fact]
    public async Task AnInboundCall_CanBeTurnedAway_AndIsRecordedAsUnanswered()
    {
        var world = await NewWorldAsync();
        var call = new ControllableCall("call-in", CallState.Ringing, CallDirection.Inbound, "+49301234567");
        await world.Service.ObserveIncomingAsync(Workspace, world.Channel, call);

        Assert.Equal(204, (await new RejectCallRouteHandler(world.Service)
            .HandleAsync(Post("calls/call-in/reject", "call-in"))).StatusCode);

        Assert.True(call.RejectCalled);
        Assert.Null(world.Store.Added[0].AnsweredAt);
        Assert.NotNull(world.Store.Added[0].EndedAt);
    }

    [Fact]
    public async Task AnOutboundCall_IsPlaced_Connects_AndEnds()
    {
        var world = await NewWorldAsync();
        var call = new ControllableCall("call-out");
        world.Channel.NextCall = call;

        var placed = await new PlaceCallRouteHandler(world.Service)
            .HandleAsync(Post("calls", callId: null, new { to = "+49301234567" }));
        Assert.Equal(201, placed.StatusCode);

        call.Transition(CallState.Connected);
        Assert.Equal(204, (await new HangupCallRouteHandler(world.Service)
            .HandleAsync(Post("calls/call-out/hangup", "call-out"))).StatusCode);

        var seen = new List<string>();
        while (world.Subscription.Events.TryRead(out var notification))
        {
            seen.Add(notification.EventName);
        }

        Assert.Equal([CallEventTypes.Placed, CallEventTypes.StateChanged, CallEventTypes.Ended], seen);
        Assert.Equal(CallOutcome.Completed, world.Store.Added[0].Outcome);
    }

    [Fact]
    public async Task AFlowAnsweringACall_ProducesTheSameRecordAsAnOperatorsClick()
    {
        // The point of routing flows through the same primitive: an automated answer is
        // indistinguishable in history from a manual one.
        var world = await NewWorldAsync();
        var call = new ControllableCall("call-in", CallState.Ringing, CallDirection.Inbound, "+49301234567");
        await world.Service.ObserveIncomingAsync(Workspace, world.Channel, call);

        await new CallAcceptActionHandler(world.Service).ExecuteAsync(
            new Core.Application.Flows.Contracts.RuleContext(
                CallEventTypes.Ringing,
                Workspace,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["callId"] = "call-in" },
                Now),
            new Dictionary<string, string>());

        Assert.True(call.AcceptCalled);
        Assert.NotNull(world.Store.Added[0].AnsweredAt);
    }

    [Fact]
    public async Task TheEventStreamTicketMintedOverRest_OpensTheWorkspacesStream()
    {
        var world = await NewWorldAsync();
        var tickets = new CallEventTicketStore(new FakeTimeProvider(Now), TimeSpan.FromMinutes(2));

        var minted = await new MintCallEventStreamRouteHandler(tickets).HandleAsync(Post("calls/event-stream", null));
        var view = Assert.IsType<CallEventStreamTicketView>(minted.Payload);

        var authorization = await new CallEventConnectTokenAuthorizer(tickets).AuthorizeAsync(
            new HostWebSocketConnectRequest(
                "communication",
                "calls/{connectToken}",
                new Dictionary<string, string> { ["connectToken"] = view.ConnectToken },
                new Dictionary<string, string[]>(),
                []));

        Assert.True(authorization.IsAuthorized);
        Assert.Equal(Workspace, authorization.Subject);
        Assert.NotNull(world.Service);
    }

    private static async Task<World> NewWorldAsync()
    {
        var registry = new CommunicationChannelRegistry();
        var store = new RecordingCallLogStore();
        var broadcaster = new CallEventBroadcaster();
        var service = new CallControlService(
            registry,
            store,
            NullLogger<CallControlService>.Instance,
            TimeProvider.System,
            mediaStreams: null,
            liveEvents: broadcaster);
        var channel = new FakeCommunicationChannel { ChannelId = "ch-1" };
        registry.Register(Workspace, channel);

        await Task.CompletedTask;
        return new World(service, store, channel, broadcaster.Subscribe(Workspace));
    }

    private sealed record World(
        CallControlService Service,
        RecordingCallLogStore Store,
        FakeCommunicationChannel Channel,
        CallEventSubscription Subscription);

    private static HostAdminApiRequest Get(string path) => Request("GET", path, null, null);

    private static HostAdminApiRequest Post(string path, string? callId, object? body = null) =>
        Request("POST", path, callId, body);

    private static HostAdminApiRequest Request(string method, string path, string? callId, object? body)
    {
        var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (callId is not null)
        {
            routeValues["callId"] = callId;
        }

        return new HostAdminApiRequest(
            "communication",
            method,
            path,
            routeValues,
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            body is null ? null : JsonSerializer.SerializeToElement(body),
            UserId: "user-1",
            WorkspaceKey: Workspace);
    }
}
