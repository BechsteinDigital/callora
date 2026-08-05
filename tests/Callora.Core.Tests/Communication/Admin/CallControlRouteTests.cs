using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Admin.Calls;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// The REST face of the call-control operations added in #116: accept, reject, DTMF, the live list
/// and the event-stream ticket. Three outcomes are kept distinct — 404 for a call the workspace does
/// not have, 409 for a call the request does not apply to, 400 for a malformed request — because a
/// client rendering a call list needs that difference.
/// </summary>
public sealed class CallControlRouteTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EveryControlRouteRequiresManage_AndEveryReadRouteRequiresRead()
    {
        var routes = CallAdminRoutes.Build(new FakeCallControlService(), NewTickets());

        Assert.All(
            routes.Where(r => r.HttpMethod == "POST" && r.RouteTemplate != "calls/event-stream"),
            r => Assert.Equal("communication.calls.manage", r.RequiredPermission));
        Assert.All(
            routes.Where(r => r.HttpMethod == "GET"),
            r => Assert.Equal("communication.calls.read", r.RequiredPermission));
        // Minting a read-only stream ticket is a read, not control.
        Assert.Equal(
            "communication.calls.read",
            routes.Single(r => r.RouteTemplate == "calls/event-stream").RequiredPermission);
    }

    [Fact]
    public void TheLiveListRouteIsDeclaredBeforeTheCallIdRoute()
    {
        // "calls/active" would otherwise be swallowed by "calls/{callId}".
        var routes = CallAdminRoutes.Build(new FakeCallControlService());

        var activeIndex = routes.ToList().FindIndex(r => r.RouteTemplate == "calls/active");
        var byIdIndex = routes.ToList().FindIndex(r => r.RouteTemplate == "calls/{callId}");
        Assert.True(activeIndex >= 0 && activeIndex < byIdIndex);
    }

    [Fact]
    public void WithoutATicketStore_NoEventStreamRouteIsDeclared()
    {
        // A route that cannot mint anything reads as a capability the deployment does not have.
        var routes = CallAdminRoutes.Build(new FakeCallControlService());

        Assert.DoesNotContain(routes, r => r.RouteTemplate == "calls/event-stream");
    }

    [Fact]
    public async Task Accept_Returns204_WhenTheCallWasAnswered()
    {
        var service = new FakeCallControlService { ControlResult = true };

        var response = await new AcceptCallRouteHandler(service).HandleAsync(Request("calls/call-1/accept"));

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(("ws-a", "call-1"), service.LastAccepted);
    }

    [Fact]
    public async Task Accept_Returns404_WhenTheWorkspaceHasNoSuchCall()
    {
        var response = await new AcceptCallRouteHandler(new FakeCallControlService { ControlResult = false })
            .HandleAsync(Request("calls/call-1/accept"));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Accept_Returns409_WhenTheCallCannotBeAnsweredInItsState()
    {
        var service = new FakeCallControlService { ControlThrows = new InvalidOperationException("not ringing") };

        var response = await new AcceptCallRouteHandler(service).HandleAsync(Request("calls/call-1/accept"));

        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Reject_Returns204_WhenTheCallWasTurnedAway()
    {
        var service = new FakeCallControlService { ControlResult = true };

        var response = await new RejectCallRouteHandler(service).HandleAsync(Request("calls/call-1/reject"));

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(("ws-a", "call-1"), service.LastRejected);
    }

    [Fact]
    public async Task Dtmf_Returns204_AndPassesTheSequence()
    {
        var service = new FakeCallControlService { ControlResult = true };

        var response = await new SendDtmfRouteHandler(service)
            .HandleAsync(Request("calls/call-1/dtmf", Body(new { tones = "123#" })));

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(("ws-a", "call-1", "123#"), service.LastDtmf);
    }

    [Fact]
    public async Task Dtmf_WithoutTones_Returns400()
    {
        var service = new FakeCallControlService { ControlResult = true };

        var response = await new SendDtmfRouteHandler(service).HandleAsync(Request("calls/call-1/dtmf", Body(new { })));

        Assert.Equal(400, response.StatusCode);
        Assert.Null(service.LastDtmf);
    }

    [Fact]
    public async Task Dtmf_WithAnInvalidTone_Returns400()
    {
        var service = new FakeCallControlService { ControlThrows = new ArgumentException("not a DTMF tone") };

        var response = await new SendDtmfRouteHandler(service)
            .HandleAsync(Request("calls/call-1/dtmf", Body(new { tones = "12X" })));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task ListActive_ReturnsTheWorkspacesLiveCalls()
    {
        var service = new FakeCallControlService
        {
            ActiveResult = [new CallSnapshot("call-1", CallDirection.Inbound, CallState.Ringing, "+49301234567")],
        };

        var response = await new ListActiveCallsRouteHandler(service).HandleAsync(Request("calls/active"));

        Assert.Equal(200, response.StatusCode);
        var view = Assert.Single(Assert.IsType<CallView[]>(response.Payload));
        Assert.Equal("call-1", view.CallId);
        Assert.Equal("Ringing", view.State);
    }

    [Fact]
    public async Task EventStream_Returns201_WithARedeemableTicket()
    {
        var tickets = NewTickets();

        var response = await new MintCallEventStreamRouteHandler(tickets).HandleAsync(Request("calls/event-stream"));

        Assert.Equal(201, response.StatusCode);
        var view = Assert.IsType<CallEventStreamTicketView>(response.Payload);
        Assert.Equal("/ws/communication/calls/" + view.ConnectToken, view.ConnectPath);
        Assert.Equal("ws-a", tickets.TryConsume(view.ConnectToken));
    }

    [Fact]
    public async Task EventStream_WithoutAWorkspace_Returns400()
    {
        var response = await new MintCallEventStreamRouteHandler(NewTickets())
            .HandleAsync(Request("calls/event-stream", workspaceKey: null));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task WithoutACallId_TheControlRoutesReturn400()
    {
        var service = new FakeCallControlService { ControlResult = true };
        var request = new HostAdminApiRequest(
            "communication", "POST", "calls//accept",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            Body: null,
            UserId: "user-1",
            WorkspaceKey: "ws-a");

        Assert.Equal(400, (await new AcceptCallRouteHandler(service).HandleAsync(request)).StatusCode);
    }

    private static CallEventTicketStore NewTickets() =>
        new(new FakeTimeProvider(Now), TimeSpan.FromMinutes(2));

    private static HostAdminApiRequest Request(
        string path, JsonElement? body = null, string? workspaceKey = "ws-a") =>
        new(
            "communication",
            path.EndsWith("active", StringComparison.Ordinal) ? "GET" : "POST",
            path,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["callId"] = "call-1" },
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            body,
            UserId: "user-1",
            WorkspaceKey: workspaceKey);

    private static JsonElement Body(object value) => JsonSerializer.SerializeToElement(value);
}
