using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Communication.Streaming;
using Callora.Plugin.Communication.Application.Admin.Streaming;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Domain.Streaming;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// The REST face of media-ticket minting (#114): the route that turns an authorized operator request
/// into a one-time credential for a live call. Before it existed, the media socket had no legitimate
/// caller at all.
/// </summary>
public sealed class MintMediaStreamRouteTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheRouteRequiresCallControlPermission()
    {
        // Minting hands out live access to a conversation, so it is a control operation, not a read.
        var routes = MediaStreamAdminRoutes.Build(
            NewMinter(LiveCall()), NullLogger<MintMediaStreamRouteHandler>.Instance);

        var route = Assert.Single(routes);
        Assert.Equal("POST", route.HttpMethod);
        Assert.Equal("calls/{callId}/media-streams", route.RouteTemplate);
        Assert.Equal("communication.calls.manage", route.RequiredPermission);
    }

    [Fact]
    public async Task ForAnOwnedLiveCall_Returns201_WithTheTicketAndItsSocketPath()
    {
        var response = await NewHandler(LiveCall()).HandleAsync(
            Request("ws-a", "call-1", new { consumerRef = "ai-agent" }));

        Assert.Equal(201, response.StatusCode);
        var view = Assert.IsType<MediaStreamTicketView>(response.Payload);
        Assert.Equal("call-1", view.CallId);
        Assert.Equal("bidirectional", view.Direction);
        Assert.Equal("/ws/communication/media/" + view.ConnectToken, view.ConnectPath);
        Assert.True(view.ExpiresInSeconds > 0);
    }

    [Fact]
    public async Task ForAnotherWorkspacesCall_Returns404()
    {
        // Identical to a call that never existed, so an operator cannot probe another workspace's
        // call ids by comparing responses.
        var callControl = new FakeCallControlService();
        callControl.LiveCalls.Add(("ws-b", "call-1"));

        var response = await NewHandler(callControl).HandleAsync(
            Request("ws-a", "call-1", new { consumerRef = "ai-agent" }));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task WithoutAWorkspace_Returns400()
    {
        var request = new HostAdminApiRequest(
            "communication", "POST", "calls/call-1/media-streams",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["callId"] = "call-1" },
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            JsonSerializer.SerializeToElement(new { consumerRef = "ai-agent" }),
            UserId: "user-1",
            WorkspaceKey: null);

        Assert.Equal(400, (await NewHandler(LiveCall()).HandleAsync(request)).StatusCode);
    }

    [Fact]
    public async Task WithoutAConsumerRef_Returns400()
    {
        var response = await NewHandler(LiveCall()).HandleAsync(Request("ws-a", "call-1", new { }));

        Assert.Equal(400, response.StatusCode);
    }

    [Theory]
    [InlineData("inbound", MediaStreamDirection.Inbound)]
    [InlineData("OUTBOUND", MediaStreamDirection.Outbound)]
    [InlineData("bidirectional", MediaStreamDirection.Bidirectional)]
    public async Task TheRequestedDirectionIsCarriedThrough(string requested, MediaStreamDirection expected)
    {
        var store = new InMemoryMediaStreamSessionStore();
        var response = await NewHandler(LiveCall(), store).HandleAsync(
            Request("ws-a", "call-1", new { consumerRef = "ai-agent", direction = requested }));

        var view = Assert.IsType<MediaStreamTicketView>(response.Payload);
        var session = await store.GetAsync("ws-a", view.SessionId);
        Assert.Equal(expected, session!.Direction);
    }

    [Fact]
    public async Task AnUnknownDirection_Returns400_RatherThanDefaultingToDuplex()
    {
        // Coercing an unrecognized value into duplex would hand out more access than was asked for.
        var response = await NewHandler(LiveCall()).HandleAsync(
            Request("ws-a", "call-1", new { consumerRef = "ai-agent", direction = "listen" }));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task AnOmittedDirection_MeansDuplex()
    {
        var response = await NewHandler(LiveCall()).HandleAsync(
            Request("ws-a", "call-1", new { consumerRef = "ai-agent" }));

        Assert.Equal("bidirectional", Assert.IsType<MediaStreamTicketView>(response.Payload).Direction);
    }

    private static FakeCallControlService LiveCall()
    {
        var callControl = new FakeCallControlService();
        callControl.LiveCalls.Add(("ws-a", "call-1"));
        return callControl;
    }

    private static MediaStreamSessionMinter NewMinter(
        FakeCallControlService callControl, IMediaStreamSessionStore? store = null) =>
        new(callControl, store ?? new InMemoryMediaStreamSessionStore(), new FakeTimeProvider(Now));

    private static MintMediaStreamRouteHandler NewHandler(
        FakeCallControlService callControl, IMediaStreamSessionStore? store = null) =>
        new(NewMinter(callControl, store), NullLogger<MintMediaStreamRouteHandler>.Instance);

    private static HostAdminApiRequest Request(string workspaceKey, string callId, object body) =>
        new(
            "communication",
            "POST",
            $"calls/{callId}/media-streams",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["callId"] = callId },
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            JsonSerializer.SerializeToElement(body),
            UserId: "user-1",
            WorkspaceKey: workspaceKey);
}
