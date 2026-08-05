using System.Net.WebSockets;
using System.Text;
using Callora.Administration.Api;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Api;

/// <summary>
/// A surface visitor's identity reaches the plugin on a WebSocket upgrade
/// (ADR-017 §9), and the origin check that has to sit in front of it: a browser
/// attaches cookies to a cross-site handshake and nothing in the browser stops it,
/// so the host does.
/// </summary>
public sealed class SurfaceWebSocketUpgradeTests
{
    private const string Host = "portal.example.de";
    private const string WorkspaceKey = "workspace-a";
    private const string SurfaceKey = "portal";
    private const string TenantKey = "tenant-a";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task ASurfaceSessionOnTheSameOrigin_ReachesThePlugin()
    {
        var route = new CallerReportingWebSocketRoute();
        var codec = new JsonSurfaceSessionCookieCodec();
        var sessions = new InMemorySurfaceSessionStore();
        await using var app = await CreateAppAsync(route, codec, sessions);
        var cookie = await SessionCookieAsync(codec, sessions);

        var reply = await ConnectAsync(app, cookie, origin: $"https://{Host}");

        Assert.Equal("authenticated:crm.example:lead-42", reply);
        Assert.IsType<AuthenticatedSurfaceCaller>(route.LastCaller);
    }

    [Fact]
    public async Task ACrossSiteHandshake_CarriesNoCaller()
    {
        var route = new CallerReportingWebSocketRoute();
        var codec = new JsonSurfaceSessionCookieCodec();
        var sessions = new InMemorySurfaceSessionStore();
        await using var app = await CreateAppAsync(route, codec, sessions);
        var cookie = await SessionCookieAsync(codec, sessions);

        var reply = await ConnectAsync(app, cookie, origin: "https://evil.example.com");

        // The cookie was sent by the browser and is perfectly valid. It is refused
        // because the page that opened the socket is not the surface.
        Assert.Equal("none", reply);
        Assert.Null(route.LastCaller);
    }

    [Fact]
    public async Task AGuestContext_ReachesThePluginAsAGuest()
    {
        var route = new CallerReportingWebSocketRoute();
        var codec = new JsonSurfaceSessionCookieCodec();
        await using var app = await CreateAppAsync(route, codec, new InMemorySurfaceSessionStore());
        var cookie = codec.Protect(Envelope(SurfaceSessionEnvelopeKind.Guest, "g-7"));

        var reply = await ConnectAsync(app, cookie, origin: $"https://{Host}");

        Assert.Equal("guest:g-7", reply);
    }

    [Fact]
    public async Task AConnectWithoutACookie_CarriesNoCaller()
    {
        var route = new CallerReportingWebSocketRoute();
        await using var app = await CreateAppAsync(
            route, new JsonSurfaceSessionCookieCodec(), new InMemorySurfaceSessionStore());

        Assert.Equal("none", await ConnectAsync(app, cookie: null, origin: null));
    }

    private static async Task<string> ConnectAsync(WebApplication app, string? cookie, string? origin)
    {
        var wsClient = app.GetTestServer().CreateWebSocketClient();
        wsClient.ConfigureRequest = request =>
        {
            if (cookie is not null)
            {
                request.Headers["Cookie"] = $"callora_surface={cookie}";
            }

            if (origin is not null)
            {
                request.Headers["Origin"] = origin;
            }
        };

        var socket = await wsClient.ConnectAsync(
            new Uri($"ws://{Host}/ws/surface-plugin/stream"), CancellationToken.None);

        var buffer = new byte[1024];
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    private static async Task<string> SessionCookieAsync(
        JsonSurfaceSessionCookieCodec codec,
        InMemorySurfaceSessionStore sessions)
    {
        var sessionId = Guid.NewGuid();
        await sessions.CreateAsync(new SurfaceSession(
            sessionId,
            TenantKey,
            WorkspaceKey,
            SurfaceKey,
            Host,
            new SurfaceSubject("crm.example", "lead-42"),
            new SurfaceIdentity(
                "Erika Muster",
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                "password",
                Now.AddMinutes(-1),
                Now.AddHours(2)),
            Now,
            Now.AddHours(2),
            "crm",
            "1.0.0"));

        return codec.Protect(Envelope(SurfaceSessionEnvelopeKind.Authenticated, sessionId.ToString("N")));
    }

    private static SurfaceSessionEnvelope Envelope(SurfaceSessionEnvelopeKind kind, string id) =>
        new(
            SurfaceSessionEnvelope.CurrentVersion,
            kind,
            id,
            TenantKey,
            WorkspaceKey,
            SurfaceKey,
            Host,
            Now);

    private static async Task<WebApplication> CreateAppAsync(
        CallerReportingWebSocketRoute route,
        JsonSurfaceSessionCookieCodec codec,
        InMemorySurfaceSessionStore sessions)
    {
        var surfaces = new InMemoryWorkspaceSurfaceStore();
        surfaces.Seed(new WorkspaceSurfaceSnapshot(
            Guid.NewGuid(), WorkspaceKey, SurfaceKey, "Portal", "spa", null, null, "/",
            SurfaceAccessMode.Mixed, "de", null, null, null, null, true, Now, Now)
        {
            TenantKey = TenantKey,
            IdentityPluginId = "crm",
            IdentityAssignedAtUtc = Now.AddDays(-1),
        });

        var contributor = new EchoWebSocketContributor
        {
            PluginId = "surface-plugin",
            WebSocketRoutes = [new HostWebSocketRouteRegistration("stream", route, route)],
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ICalloraPluginCatalog>(new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>>
            {
                [typeof(IHostWebSocketEndpointContributor)] = [contributor],
            }));
        builder.Services.AddSingleton(new SurfaceIdentityOptions());
        builder.Services.AddSingleton<ISurfaceSessionCookieCodec>(codec);
        builder.Services.AddSingleton<ISurfaceSessionStore>(sessions);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(surfaces);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<SurfaceSessionCookieAccessor>();
        builder.Services.AddScoped<SurfaceSessionAuthenticator>();
        builder.Services.AddScoped<SurfaceUpgradeCallerResolver>();
        builder.Services.AddSingleton(NullLogger<SurfaceUpgradeCallerResolver>.Instance);

        var app = builder.Build();
        app.UseWebSockets();
        app.MapPluginWebSocketEndpoints();
        await app.StartAsync();
        return app;
    }
}
