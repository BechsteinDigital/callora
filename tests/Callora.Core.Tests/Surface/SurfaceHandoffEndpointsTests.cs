using System.Net;
using System.Net.Http.Json;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Callora.Core.Tests.Support;
using Callora.Surface.Rendering.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// The handover over HTTP (ADR-017 §8.4): the source surface mints a ticket, the
/// target host exchanges it for a session of its own. Two surfaces, two hosts, one
/// visitor.
/// </summary>
public sealed class SurfaceHandoffEndpointsTests
{
    private const string SourceHost = "crm.example.de";
    private const string TargetHost = "meet.example.de";
    private const string WorkspaceKey = "workspace-a";
    private const string TenantKey = "tenant-a";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task ASessionOnOneHostBecomesASessionOnTheOther()
    {
        var codec = new JsonSurfaceSessionCookieCodec();
        var sessions = new InMemorySurfaceSessionStore();
        await using var app = await CreateAppAsync(codec, sessions);
        var cookie = await SessionCookieAsync(codec, sessions);

        var ticket = await IssueAsync(app, cookie, origin: $"https://{SourceHost}", returnPath: "/room/7");

        Assert.StartsWith($"http://{TargetHost}/surface/handoff/redeem", ticket.RedeemUrl, StringComparison.Ordinal);

        var redeemed = await RedeemAsync(app, ticket.RedeemUrl);

        Assert.Equal(HttpStatusCode.Redirect, redeemed.StatusCode);
        Assert.Equal("/room/7", redeemed.Headers.Location!.ToString());
        Assert.Contains(
            "callora_surface=",
            string.Join("; ", redeemed.Headers.GetValues("Set-Cookie")),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATicketCannotBeRedeemedTwice()
    {
        var codec = new JsonSurfaceSessionCookieCodec();
        var sessions = new InMemorySurfaceSessionStore();
        await using var app = await CreateAppAsync(codec, sessions);
        var cookie = await SessionCookieAsync(codec, sessions);
        var ticket = await IssueAsync(app, cookie, origin: $"https://{SourceHost}", returnPath: "/");

        _ = await RedeemAsync(app, ticket.RedeemUrl);
        var replay = await RedeemAsync(app, ticket.RedeemUrl);

        Assert.Equal(HttpStatusCode.Forbidden, replay.StatusCode);
    }

    [Fact]
    public async Task AnotherSiteCannotMintATicketOutOfTheVisitorsCookie()
    {
        var codec = new JsonSurfaceSessionCookieCodec();
        var sessions = new InMemorySurfaceSessionStore();
        await using var app = await CreateAppAsync(codec, sessions);
        var cookie = await SessionCookieAsync(codec, sessions);

        var response = await PostTicketAsync(app, cookie, origin: "https://evil.example.com", returnPath: "/");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WithoutASurfaceSessionThereIsNothingToHandOver()
    {
        var codec = new JsonSurfaceSessionCookieCodec();
        await using var app = await CreateAppAsync(codec, new InMemorySurfaceSessionStore());

        var response = await PostTicketAsync(app, cookie: null, origin: $"https://{SourceHost}", returnPath: "/");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnAbsoluteReturnPathIsReplacedByTheTargetRoot()
    {
        var codec = new JsonSurfaceSessionCookieCodec();
        var sessions = new InMemorySurfaceSessionStore();
        await using var app = await CreateAppAsync(codec, sessions);
        var cookie = await SessionCookieAsync(codec, sessions);

        var ticket = await IssueAsync(
            app, cookie, origin: $"https://{SourceHost}", returnPath: "https://evil.example.com/steal");
        var redeemed = await RedeemAsync(app, ticket.RedeemUrl);

        // The issuer does not get to choose where the visitor lands.
        Assert.Equal("/", redeemed.Headers.Location!.ToString());
    }

    private static async Task<SurfaceHandoffTicketApiResponse> IssueAsync(
        WebApplication app,
        string? cookie,
        string origin,
        string returnPath)
    {
        var response = await PostTicketAsync(app, cookie, origin, returnPath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SurfaceHandoffTicketApiResponse>())!;
    }

    private static async Task<HttpResponseMessage> PostTicketAsync(
        WebApplication app,
        string? cookie,
        string origin,
        string returnPath)
    {
        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://{SourceHost}/surface/handoff/tickets")
        {
            Content = JsonContent.Create(new SurfaceHandoffTicketApiRequest("meet", returnPath)),
        };
        request.Headers.Add("Origin", origin);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", $"callora_surface={cookie}");
        }

        return await client.SendAsync(request);
    }

    // The redeem URL is absolute and names the target host, so the same test client
    // reaches the other surface exactly as a browser following the redirect would.
    private static Task<HttpResponseMessage> RedeemAsync(WebApplication app, string redeemUrl) =>
        app.GetTestClient().GetAsync(redeemUrl);

    private static async Task<string> SessionCookieAsync(
        JsonSurfaceSessionCookieCodec codec,
        InMemorySurfaceSessionStore sessions)
    {
        var sessionId = Guid.NewGuid();
        await sessions.CreateAsync(new SurfaceSession(
            sessionId,
            TenantKey,
            WorkspaceKey,
            "crm",
            SourceHost,
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

        return codec.Protect(new SurfaceSessionEnvelope(
            SurfaceSessionEnvelope.CurrentVersion,
            SurfaceSessionEnvelopeKind.Authenticated,
            sessionId.ToString("N"),
            TenantKey,
            WorkspaceKey,
            "crm",
            SourceHost,
            Now));
    }

    private static async Task<WebApplication> CreateAppAsync(
        JsonSurfaceSessionCookieCodec codec,
        InMemorySurfaceSessionStore sessions)
    {
        var surfaces = new InMemoryWorkspaceSurfaceStore();
        surfaces.Seed(SurfaceSnapshot("crm", SourceHost));
        surfaces.Seed(SurfaceSnapshot("meet", TargetHost));

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new SurfaceIdentityOptions());
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ISurfaceSessionCookieCodec>(codec);
        builder.Services.AddSingleton<ISurfaceSessionStore>(sessions);
        builder.Services.AddSingleton<ISurfaceHandoffTicketStore, InMemorySurfaceHandoffTicketStore>();
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(surfaces);
        builder.Services.AddSingleton<IBusinessEventBus>(new RecordingBusinessEventBus());
        builder.Services.AddSingleton<SurfaceSessionCookieAccessor>();
        builder.Services.AddScoped<SurfaceSessionAuthenticator>();
        builder.Services.AddScoped<SurfaceHandoffService>();
        builder.Services.AddScoped<SurfaceSessionService>();
        builder.Services.AddSingleton(NullLogger<SurfaceSessionService>.Instance);

        var app = builder.Build();
        app.MapSurfaceHandoffEndpoints();
        await app.StartAsync();
        return app;
    }

    private static WorkspaceSurfaceSnapshot SurfaceSnapshot(string surfaceKey, string host) =>
        new(
            Guid.NewGuid(), WorkspaceKey, surfaceKey, surfaceKey, "spa",
            null, host, "/", SurfaceAccessMode.Authenticated, "de",
            null, null, null, null, true, Now, Now)
        {
            TenantKey = TenantKey,
            IdentityPluginId = "crm",
            IdentityAssignedAtUtc = Now.AddDays(-1),
        };
}
