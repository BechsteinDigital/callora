using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Admin.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// The REST face of the call-control primitive: the route handlers resolve the caller's workspace,
/// parse/validate input and map <see cref="ICallControlService"/> results to Admin-API responses —
/// workspace-scoped, with clean status codes.
/// </summary>
public sealed class CallAdminRoutesTests
{
    private static readonly PlaceCallRouteHandler PlaceHandler = new(new FakeCallControlService());

    [Fact]
    public void Routes_DeclarePermissions_ReadForQueries_ManageForControl()
    {
        var routes = CallAdminRoutes.Build(new FakeCallControlService());

        Assert.Contains(routes, r => r is { HttpMethod: "POST", RouteTemplate: "calls" } && r.RequiredPermission == "communication.calls.manage");
        Assert.Contains(routes, r => r is { HttpMethod: "POST", RouteTemplate: "calls/{callId}/hangup" } && r.RequiredPermission == "communication.calls.manage");
        Assert.Contains(routes, r => r is { HttpMethod: "GET", RouteTemplate: "calls" } && r.RequiredPermission == "communication.calls.read");
        Assert.Contains(routes, r => r is { HttpMethod: "GET", RouteTemplate: "calls/{callId}" } && r.RequiredPermission == "communication.calls.read");
    }

    // --- Place ---

    [Fact]
    public async Task Place_ValidBody_Returns201_WithCallView()
    {
        var service = new FakeCallControlService
        {
            PlaceResult = new CallSnapshot("call-1", CallDirection.Outbound, CallState.Connecting, "+49301234567"),
        };
        var handler = new PlaceCallRouteHandler(service);

        var response = await handler.HandleAsync(Request(
            "POST", "calls", Body(new { to = "+49301234567", channelId = "ch-1" }), workspaceKey: "ws-a"));

        Assert.Equal(201, response.StatusCode);
        var view = Assert.IsType<CallView>(response.Payload);
        Assert.Equal("call-1", view.CallId);
        Assert.Equal("Outbound", view.Direction);
        Assert.Equal("Connecting", view.State);
        Assert.Equal("ws-a", service.LastPlaced!.WorkspaceKey);
        Assert.Equal("+49301234567", service.LastPlaced.To);
        Assert.Equal("ch-1", service.LastPlaced.ChannelId);
    }

    [Fact]
    public async Task Place_MissingTo_Returns400()
    {
        var response = await PlaceHandler.HandleAsync(Request(
            "POST", "calls", Body(new { channelId = "ch-1" }), workspaceKey: "ws-a"));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Place_NoBody_Returns400()
    {
        var response = await PlaceHandler.HandleAsync(Request("POST", "calls", body: null, workspaceKey: "ws-a"));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Place_WithoutWorkspace_Returns400()
    {
        // Platform operator with no bound workspace and no ?workspaceKey= → rejected, never defaulted.
        var response = await PlaceHandler.HandleAsync(Request(
            "POST", "calls", Body(new { to = "+49301234567" }), workspaceKey: null));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Place_NoVoiceChannel_Returns409()
    {
        var service = new FakeCallControlService { PlaceThrows = new InvalidOperationException("No voice-capable channel.") };
        var handler = new PlaceCallRouteHandler(service);

        var response = await handler.HandleAsync(Request(
            "POST", "calls", Body(new { to = "+49301234567" }), workspaceKey: "ws-a"));

        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Place_TokenBoundWorkspace_WinsOverQuery()
    {
        var service = new FakeCallControlService
        {
            PlaceResult = new CallSnapshot("call-1", CallDirection.Outbound, CallState.Connecting, "+49301234567"),
        };
        var handler = new PlaceCallRouteHandler(service);

        await handler.HandleAsync(Request(
            "POST", "calls", Body(new { to = "+49301234567" }),
            workspaceKey: "ws-token",
            query: new() { ["workspaceKey"] = ["ws-query"] }));

        Assert.Equal("ws-token", service.LastPlaced!.WorkspaceKey); // token-bound scope is authoritative
    }

    [Fact]
    public async Task Place_PlatformOperator_UsesTheHostResolvedWorkspace()
    {
        var service = new FakeCallControlService
        {
            PlaceResult = new CallSnapshot("call-1", CallDirection.Outbound, CallState.Connecting, "+49301234567"),
        };
        var handler = new PlaceCallRouteHandler(service);

        // The host resolves ?workspaceKey= into WorkspaceKey and gates availability
        // there before dispatching (#109); the handler reads only that value.
        await handler.HandleAsync(Request(
            "POST", "calls", Body(new { to = "+49301234567" }),
            workspaceKey: "ws-query"));

        Assert.Equal("ws-query", service.LastPlaced!.WorkspaceKey);
    }

    [Fact]
    public async Task Place_WithoutAHostResolvedWorkspace_Returns400()
    {
        var handler = new PlaceCallRouteHandler(new FakeCallControlService());

        // A raw query value never substitutes for the host-resolved workspace —
        // honouring it here would bypass the availability gate (#109).
        var response = await handler.HandleAsync(Request(
            "POST", "calls", Body(new { to = "+49301234567" }),
            workspaceKey: null,
            query: new() { ["workspaceKey"] = ["ws-query"] }));

        Assert.Equal(400, response.StatusCode);
    }

    // --- Get ---

    [Fact]
    public async Task Get_TrackedCall_Returns200()
    {
        var service = new FakeCallControlService
        {
            GetResult = new CallSnapshot("call-1", CallDirection.Outbound, CallState.Connected, "+49301234567"),
        };
        var handler = new GetCallRouteHandler(service);

        var response = await handler.HandleAsync(Request(
            "GET", "calls/{callId}", workspaceKey: "ws-a", routeValues: new() { ["callId"] = "call-1" }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("Connected", Assert.IsType<CallView>(response.Payload).State);
    }

    [Fact]
    public async Task Get_UnknownCall_Returns404()
    {
        var handler = new GetCallRouteHandler(new FakeCallControlService { GetResult = null });

        var response = await handler.HandleAsync(Request(
            "GET", "calls/{callId}", workspaceKey: "ws-a", routeValues: new() { ["callId"] = "nope" }));

        Assert.Equal(404, response.StatusCode);
    }

    // --- Hangup ---

    [Fact]
    public async Task Hangup_TrackedCall_Returns204()
    {
        var service = new FakeCallControlService { HangupResult = true };
        var handler = new HangupCallRouteHandler(service);

        var response = await handler.HandleAsync(Request(
            "POST", "calls/{callId}/hangup", workspaceKey: "ws-a", routeValues: new() { ["callId"] = "call-1" }));

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(("ws-a", "call-1"), service.LastHangup);
    }

    [Fact]
    public async Task Hangup_UnknownCall_Returns404()
    {
        var handler = new HangupCallRouteHandler(new FakeCallControlService { HangupResult = false });

        var response = await handler.HandleAsync(Request(
            "POST", "calls/{callId}/hangup", workspaceKey: "ws-a", routeValues: new() { ["callId"] = "nope" }));

        Assert.Equal(404, response.StatusCode);
    }

    // --- List ---

    [Fact]
    public async Task List_ReturnsHistory_200()
    {
        var service = new FakeCallControlService
        {
            HistoryResult =
            [
                new CallHistoryEntry("call-1", "Outbound", "+49301234567", DateTimeOffset.UnixEpoch, null, null, 0, "InProgress", null, []),
            ],
        };
        var handler = new ListCallsRouteHandler(service);

        var response = await handler.HandleAsync(Request("GET", "calls", workspaceKey: "ws-a"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(50, service.LastListLimit); // default page size
    }

    [Fact]
    public async Task List_LimitIsCapped()
    {
        var service = new FakeCallControlService();
        var handler = new ListCallsRouteHandler(service);

        await handler.HandleAsync(Request(
            "GET", "calls", workspaceKey: "ws-a", query: new() { ["limit"] = ["9999"] }));

        Assert.Equal(200, service.LastListLimit); // hard cap, no unbounded scan
    }

    private static HostAdminApiRequest Request(
        string method,
        string path,
        JsonElement? body = null,
        string? workspaceKey = null,
        Dictionary<string, string[]>? query = null,
        Dictionary<string, string>? routeValues = null) =>
        new(
            "communication",
            method,
            path,
            routeValues ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            query ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            body,
            UserId: "user-1",
            WorkspaceKey: workspaceKey);

    private static JsonElement Body(object value) => JsonSerializer.SerializeToElement(value);
}

/// <summary>Records the call-control calls the route handlers make and returns configured results.</summary>
internal sealed class FakeCallControlService : ICallControlService
{
    public PlaceCallCommand? LastPlaced { get; private set; }

    public CallSnapshot? PlaceResult { get; set; }

    public Exception? PlaceThrows { get; set; }

    public (string Workspace, string CallId)? LastHangup { get; private set; }

    public bool HangupResult { get; set; }

    public CallSnapshot? GetResult { get; set; }

    public IReadOnlyList<CallHistoryEntry> HistoryResult { get; set; } = [];

    public int? LastListLimit { get; private set; }

    public Task<CallSnapshot> PlaceCallAsync(PlaceCallCommand command, CancellationToken cancellationToken = default)
    {
        LastPlaced = command;
        if (PlaceThrows is not null)
        {
            throw PlaceThrows;
        }

        return Task.FromResult(PlaceResult
            ?? new CallSnapshot(command.To, CallDirection.Outbound, CallState.Connecting, command.To));
    }

    public Task<bool> HangupAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default)
    {
        LastHangup = (workspaceKey, callId);
        return Task.FromResult(HangupResult);
    }

    /// <summary>Set to make the next control operation report a rejected state transition.</summary>
    public Exception? ControlThrows { get; set; }

    public (string Workspace, string CallId)? LastAccepted { get; private set; }

    public (string Workspace, string CallId)? LastRejected { get; private set; }

    public (string Workspace, string CallId, string Tones)? LastDtmf { get; private set; }

    /// <summary>Outcome the accept/reject/DTMF operations report; hang-up keeps its own flag.</summary>
    public bool ControlResult { get; set; }

    public IReadOnlyList<CallSnapshot> ActiveResult { get; set; } = [];

    public Task<bool> AcceptAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default)
    {
        LastAccepted = (workspaceKey, callId);
        return ControlThrows is not null ? Task.FromException<bool>(ControlThrows) : Task.FromResult(ControlResult);
    }

    public Task<bool> RejectAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default)
    {
        LastRejected = (workspaceKey, callId);
        return ControlThrows is not null ? Task.FromException<bool>(ControlThrows) : Task.FromResult(ControlResult);
    }

    public Task<bool> SendDtmfAsync(
        string workspaceKey, string callId, string tones, CancellationToken cancellationToken = default)
    {
        LastDtmf = (workspaceKey, callId, tones);
        return ControlThrows is not null ? Task.FromException<bool>(ControlThrows) : Task.FromResult(ControlResult);
    }

    public IReadOnlyList<CallSnapshot> ListActive(string workspaceKey) => ActiveResult;

    /// <summary>
    /// Calls this fake considers live. Empty means "answer <see cref="GetResult"/> for anything",
    /// which is what the older route tests expect; adding entries makes the fake workspace- and
    /// call-aware, which is what the ticket-minting tests need.
    /// </summary>
    public HashSet<(string WorkspaceKey, string CallId)> LiveCalls { get; } = [];

    public CallSnapshot? Get(string workspaceKey, string callId)
    {
        if (LiveCalls.Count == 0)
        {
            return GetResult;
        }

        return LiveCalls.Contains((workspaceKey, callId))
            ? GetResult ?? new CallSnapshot(callId, CallDirection.Inbound, CallState.Connected, "+49301234567")
            : null;
    }

    public Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(
        string workspaceKey, int limit, CancellationToken cancellationToken = default)
    {
        LastListLimit = limit;
        return Task.FromResult(HistoryResult);
    }
}
