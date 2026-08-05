using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Admin.WebRtc;
using Callora.Plugin.Communication.Application.WebRtc;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Communication.WebRtc;

/// <summary>
/// The browser-facing entry to WebRTC (#114). The minter primitive existed but had no production
/// caller, so a browser had no legitimate way to obtain a signalling ticket or the ICE configuration
/// it needs alongside it.
/// </summary>
public sealed class MintWebRtcSessionRouteTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TheRouteRequiresCallControlPermission()
    {
        var routes = WebRtcAdminRoutes.Build(
            new FakeWebRtcSessionMinter(),
            ReadyProbe(),
            IceConfigurationOptions.None,
            new FakeTimeProvider(Now),
            NullLogger<MintWebRtcSessionRouteHandler>.Instance);

        var route = Assert.Single(routes);
        Assert.Equal("POST", route.HttpMethod);
        Assert.Equal("webrtc/sessions", route.RouteTemplate);
        Assert.Equal("communication.calls.manage", route.RequiredPermission);
    }

    [Fact]
    public async Task Returns201_WithTheTicketAndItsSignallingPath()
    {
        var minter = new FakeWebRtcSessionMinter();
        var response = await NewHandler(minter).HandleAsync(Request("ws-a", new { target = "browser-1" }));

        Assert.Equal(201, response.StatusCode);
        var view = Assert.IsType<WebRtcSessionView>(response.Payload);
        Assert.Equal("/ws/communication/webrtc/" + view.ConnectToken, view.ConnectPath);
        Assert.Equal("ws-a", minter.LastWorkspaceKey);
        Assert.Equal("browser-1", minter.LastTarget!.Value);
    }

    [Fact]
    public async Task TheWorkspaceComesFromTheHost_NotTheBody()
    {
        // A body-supplied workspace would bypass the host's scope resolution entirely.
        var minter = new FakeWebRtcSessionMinter();

        await NewHandler(minter).HandleAsync(Request("ws-a", new { workspaceKey = "ws-b", target = "browser-1" }));

        Assert.Equal("ws-a", minter.LastWorkspaceKey);
    }

    [Fact]
    public async Task WithoutAWorkspace_Returns400()
    {
        var request = new HostAdminApiRequest(
            "communication", "POST", "webrtc/sessions",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            JsonSerializer.SerializeToElement(new { target = "browser-1" }),
            UserId: "user-1",
            WorkspaceKey: null);

        Assert.Equal(400, (await NewHandler(new FakeWebRtcSessionMinter()).HandleAsync(request)).StatusCode);
    }

    [Fact]
    public async Task WhenCommunicationIsUnavailable_Returns503_WithoutMinting()
    {
        // A ticket for a surface that cannot carry a call is a two-minute wait ending in a failed
        // socket; saying so immediately is the useful answer.
        var minter = new FakeWebRtcSessionMinter();
        var handler = new MintWebRtcSessionRouteHandler(
            minter,
            new CommunicationReadinessProbe(new CommunicationChannelRegistry()), // no channel registered → down
            IceConfigurationOptions.None,
            new FakeTimeProvider(Now),
            NullLogger<MintWebRtcSessionRouteHandler>.Instance);

        var response = await handler.HandleAsync(Request("ws-a", new { target = "browser-1" }));

        Assert.Equal(503, response.StatusCode);
        Assert.Null(minter.LastWorkspaceKey);
    }

    [Fact]
    public async Task ShortLivedTurnCredentialsAreIssuedWithTheirLifetime()
    {
        var ice = new IceConfigurationOptions(
            [new IceServerSetting("turn:turn.example.com:3478?transport=udp", SharedSecret: "s3cr3t")],
            TimeSpan.FromMinutes(10));

        var response = await NewHandler(new FakeWebRtcSessionMinter(), ice)
            .HandleAsync(Request("ws-a", new { target = "browser-1" }));

        var view = Assert.IsType<WebRtcSessionView>(response.Payload);
        var server = Assert.Single(view.IceServers);
        Assert.StartsWith($"{Now.AddMinutes(10).ToUnixTimeSeconds()}:", server.Username, StringComparison.Ordinal);
        Assert.Equal(600, view.IceCredentialExpiresInSeconds);
    }

    [Fact]
    public async Task WithoutASharedSecret_NoCredentialLifetimeIsClaimed()
    {
        // Reporting a lifetime for a static password would be a promise the deployment does not keep.
        var ice = new IceConfigurationOptions(
            [new IceServerSetting("turn:turn.example.com:3478?transport=udp", Username: "user", Credential: "pass")],
            TimeSpan.FromMinutes(10));

        var response = await NewHandler(new FakeWebRtcSessionMinter(), ice)
            .HandleAsync(Request("ws-a", new { target = "browser-1" }));

        Assert.Null(Assert.IsType<WebRtcSessionView>(response.Payload).IceCredentialExpiresInSeconds);
    }

    private static CommunicationReadinessProbe ReadyProbe()
    {
        var registry = new CommunicationChannelRegistry();
        registry.Register("ws-a", new FakeVoiceChannel { ChannelId = "webrtc-ws-a" });
        return new CommunicationReadinessProbe(registry, accountStore: null, webRtcConfigured: true);
    }

    private static MintWebRtcSessionRouteHandler NewHandler(
        IWebRtcSessionMinter minter, IceConfigurationOptions? ice = null) =>
        new(minter,
            ReadyProbe(),
            ice ?? IceConfigurationOptions.None,
            new FakeTimeProvider(Now),
            NullLogger<MintWebRtcSessionRouteHandler>.Instance);

    private static HostAdminApiRequest Request(string? workspaceKey, object body) =>
        new(
            "communication",
            "POST",
            "webrtc/sessions",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
            JsonSerializer.SerializeToElement(body),
            UserId: "user-1",
            WorkspaceKey: workspaceKey);
}

/// <summary>Records what the route asked the minter for and returns a fixed ticket.</summary>
internal sealed class FakeWebRtcSessionMinter : IWebRtcSessionMinter
{
    public string? LastWorkspaceKey { get; private set; }

    public CallTarget? LastTarget { get; private set; }

    public string? LastCallId { get; private set; }

    public WebRtcSessionTicket MintSession(string workspaceKey, CallTarget target, string? callId = null)
    {
        LastWorkspaceKey = workspaceKey;
        LastTarget = target;
        LastCallId = callId;
        return new WebRtcSessionTicket("signalling-token", 120);
    }
}
