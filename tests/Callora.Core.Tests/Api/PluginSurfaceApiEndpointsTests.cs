using System.Net;
using System.Net.Http.Json;
using System.Text;
using Callora.Administration.Api;
using Callora.Core.Application.Audit;
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
/// The surface API seam end to end (#125 block B). The host answers the questions
/// only it can — valid context, right host, plugin available, route mounted, request
/// within its limits, execution recorded — and hands the rest to the plugin.
/// </summary>
public sealed class PluginSurfaceApiEndpointsTests
{
    private const string Host = "portal.example.de";
    private const string PluginId = "crm";
    private const string WorkspaceKey = "workspace-a";
    private const string SurfaceKey = "portal";
    private const string TenantKey = "tenant-a";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task AnAuthenticatedVisitorReachesTheHandlerWithTheirIdentity()
    {
        var handler = new StaticSurfaceApiRouteHandler(200, new { ok = true });
        var fixture = new SurfaceApiFixture(Route("GET", "leads/{leadId}", handler));
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "leads/42", await fixture.SessionCookieAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var request = handler.LastRequest!;
        Assert.Equal(WorkspaceKey, request.WorkspaceKey);
        Assert.Equal(SurfaceKey, request.SurfaceKey);
        Assert.Equal(TenantKey, request.TenantKey);
        Assert.Equal("42", request.RouteValues["leadId"]);
        var caller = Assert.IsType<AuthenticatedSurfaceCaller>(request.Caller);
        Assert.Equal("crm.example", caller.Subject.Issuer);
        Assert.Equal(["agent"], caller.Identity.Claims["crm.roles"]);
        Assert.False(string.IsNullOrWhiteSpace(request.RequestId));
    }

    [Fact]
    public async Task WithoutASurfaceContextThereIsNoCaller()
    {
        var fixture = new SurfaceApiFixture(Route("GET", "leads", new StaticSurfaceApiRouteHandler(200)));
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "leads", cookie: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AGuestCannotReachAnAuthenticatedRoute()
    {
        var fixture = new SurfaceApiFixture(Route("GET", "leads", new StaticSurfaceApiRouteHandler(200)));
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "leads", fixture.GuestCookie());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AGuestReachesARouteThatOptedIn()
    {
        var handler = new StaticSurfaceApiRouteHandler(200);
        var fixture = new SurfaceApiFixture(
            Route("GET", "cart", handler, SurfaceApiRouteAudience.GuestOrAuthenticated));
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "cart", fixture.GuestCookie());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // The guest arrives as a guest, so a handler cannot mistake it for an identity.
        Assert.IsType<GuestSurfaceCaller>(handler.LastRequest!.Caller);
    }

    [Fact]
    public async Task ACrossSiteRequestIsRefusedBeforeTheCookieIsRead()
    {
        var fixture = new SurfaceApiFixture(Route("GET", "leads", new StaticSurfaceApiRouteHandler(200)));
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(
            app, HttpMethod.Get, "leads", await fixture.SessionCookieAsync(), origin: "https://evil.example.com");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnavailablePluginIsIndistinguishableFromAMissingRoute()
    {
        var fixture = new SurfaceApiFixture(Route("GET", "leads", new StaticSurfaceApiRouteHandler(200)))
        {
            UnavailablePluginIds = [PluginId],
        };
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "leads", await fixture.SessionCookieAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ARefusedRouteCannotBeReachedByGuessingItsPath()
    {
        // Declared with a traversal segment, so the inventory never mounts it.
        var fixture = new SurfaceApiFixture(
            Route("GET", "../admin", new StaticSurfaceApiRouteHandler(200)));
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "../admin", await fixture.SessionCookieAsync());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnOversizedBodyNeverReachesTheHandler()
    {
        var handler = new StaticSurfaceApiRouteHandler(200);
        var fixture = new SurfaceApiFixture(Route("POST", "leads", handler))
        {
            Options = new SurfaceApiOptions { MaxRequestBodyBytes = 64 },
        };
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(
            app, HttpMethod.Post, "leads", await fixture.SessionCookieAsync(),
            body: new string('x', 512));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task AStallingHandlerHitsTheDeadline()
    {
        var fixture = new SurfaceApiFixture(Route("GET", "slow", StaticSurfaceApiRouteHandler.Stalling()))
        {
            Options = new SurfaceApiOptions { HandlerTimeout = TimeSpan.FromMilliseconds(50) },
        };
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "slow", await fixture.SessionCookieAsync());

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
    }

    [Fact]
    public async Task AThrowingHandlerAnswersWithoutDetail()
    {
        var fixture = new SurfaceApiFixture(Route("GET", "boom", StaticSurfaceApiRouteHandler.Throwing()));
        await using var app = await fixture.StartAsync();

        var response = await fixture.SendAsync(app, HttpMethod.Get, "boom", await fixture.SessionCookieAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain(
            "exploded", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EveryExecutionIsAuditedWithItsProvenance()
    {
        var fixture = new SurfaceApiFixture(Route("GET", "leads", new StaticSurfaceApiRouteHandler(200)));
        await using var app = await fixture.StartAsync();

        await fixture.SendAsync(app, HttpMethod.Get, "leads", await fixture.SessionCookieAsync());

        var entry = Assert.Single(await fixture.Audit.GetRecentAsync());
        Assert.Equal("surface-api.request", entry.Action);
        Assert.Equal(PluginId, entry.PluginId);
        Assert.True(entry.IsSuccess);
        Assert.Equal("crm.example|lead-42", entry.RequestedBy);
        Assert.Equal(WorkspaceKey, entry.Metadata!["workspaceKey"]);
        Assert.Equal(SurfaceKey, entry.Metadata["surfaceKey"]);
    }

    private static HostSurfaceApiRouteRegistration Route(
        string method,
        string template,
        IHostSurfaceApiRouteHandler handler,
        SurfaceApiRouteAudience audience = SurfaceApiRouteAudience.Authenticated) =>
        new(method, template, handler, audience);

    private sealed class SurfaceApiFixture(HostSurfaceApiRouteRegistration route)
    {
        private readonly JsonSurfaceSessionCookieCodec _codec = new();
        private readonly InMemorySurfaceSessionStore _sessions = new();

        public string[] UnavailablePluginIds { get; init; } = [];

        public SurfaceApiOptions Options { get; init; } = new();

        public InMemoryHostAuditStore Audit { get; } = new();

        public async Task<WebApplication> StartAsync()
        {
            var surfaces = new InMemoryWorkspaceSurfaceStore();
            surfaces.Seed(new WorkspaceSurfaceSnapshot(
                Guid.NewGuid(), WorkspaceKey, SurfaceKey, "Portal", "spa", null, Host, "/",
                SurfaceAccessMode.Mixed, SurfaceRouting.Tree, "de", null, null, null, null, true, Now, Now)
            {
                TenantKey = TenantKey,
                IdentityPluginId = PluginId,
                IdentityAssignedAtUtc = Now.AddDays(-1),
            });

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<ICalloraPluginCatalog>(new StaticPluginCatalog(
                new Dictionary<Type, IReadOnlyList<object>>
                {
                    [typeof(IHostSurfaceApiContributor)] = [new StaticSurfaceApiContributor(PluginId, [route])],
                }));
            builder.Services.AddSingleton<IPluginAvailabilityEvaluator>(
                new StaticPluginAvailabilityEvaluator(UnavailablePluginIds));
            builder.Services.AddSingleton<IHostAuditStore>(Audit);
            builder.Services.AddSingleton(Options);
            builder.Services.AddSingleton(new SurfaceIdentityOptions());
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<ISurfaceSessionCookieCodec>(_codec);
            builder.Services.AddSingleton<ISurfaceSessionStore>(_sessions);
            builder.Services.AddSingleton<IWorkspaceSurfaceStore>(surfaces);
            builder.Services.AddSingleton<SurfaceSessionCookieAccessor>();
            builder.Services.AddScoped<SurfaceSessionAuthenticator>();
            builder.Services.AddSingleton(NullLoggerFactory.Instance);

            var app = builder.Build();
            app.MapPluginSurfaceApiEndpoints();
            await app.StartAsync();
            return app;
        }

        public Task<HttpResponseMessage> SendAsync(
            WebApplication app,
            HttpMethod method,
            string routePath,
            string? cookie,
            string? origin = null,
            string? body = null)
        {
            var request = new HttpRequestMessage(
                method, $"http://{Host}{SurfaceApiRouteRules.Prefix}/{PluginId}/{routePath}");
            request.Headers.Add("Origin", origin ?? $"https://{Host}");
            if (cookie is not null)
            {
                request.Headers.Add("Cookie", $"callora_surface={cookie}");
            }

            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return app.GetTestClient().SendAsync(request);
        }

        public string GuestCookie() =>
            _codec.Protect(Envelope(SurfaceSessionEnvelopeKind.Guest, "g-1"));

        public async Task<string> SessionCookieAsync()
        {
            var sessionId = Guid.NewGuid();
            await _sessions.CreateAsync(new SurfaceSession(
                sessionId,
                TenantKey,
                WorkspaceKey,
                SurfaceKey,
                Host,
                new SurfaceSubject("crm.example", "lead-42"),
                new SurfaceIdentity(
                    "Erika Muster",
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                    {
                        ["crm.roles"] = ["agent"],
                    },
                    "password",
                    Now.AddMinutes(-1),
                    Now.AddHours(2)),
                Now,
                Now.AddHours(2),
                PluginId,
                "1.0.0"));

            return _codec.Protect(Envelope(SurfaceSessionEnvelopeKind.Authenticated, sessionId.ToString("N")));
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
    }
}
