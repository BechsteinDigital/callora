using System.Net;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Callora.Core.Tests.Support;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// The render path with the identity subsystem composed (ADR-017 §6.1, §9): who the
/// visitor is decides whether the surface is served at all, and the answer travels
/// into the rendered document so islands can read it without a second round-trip.
/// </summary>
[Collection(SurfaceRenderingCollection.Name)]
public sealed class SurfaceIdentityRenderEndpointTests
{
    private const string PluginId = "crm";
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task APublicSurface_RendersAGuestCallerAndSetsTheContextCookie()
    {
        await using var app = await CreateAppAsync(SurfaceAuthentication.Public);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-caller-state=\"guest\"", html, StringComparison.Ordinal);
        Assert.Contains("callora_surface=", CookieHeader(response), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReturningVisitor_KeepsItsGuestSubject()
    {
        await using var app = await CreateAppAsync(SurfaceAuthentication.Public);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var first = await client.GetAsync("/surface/render");
        var cookie = ExtractCookie(first);
        var firstSubject = SubjectOf(await first.Content.ReadAsStringAsync());

        var request = new HttpRequestMessage(HttpMethod.Get, "/surface/render");
        request.Headers.Add("Cookie", cookie);
        var second = await client.SendAsync(request);

        Assert.Equal(firstSubject, SubjectOf(await second.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task AnAdministrationSurface_RedirectsToTheHostLogin()
    {
        await using var app = await CreateAppAsync(SurfaceAuthentication.Administration);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAdministrationSurface_AcceptsTheBackendPrincipal()
    {
        await using var app = await CreateAppAsync(
            SurfaceAuthentication.Administration,
            hostIdentity: HostSurfaceIdentityResult.Identified(
                SurfaceIdentityIssuers.Host, "operator-7", "backend-session", Now.AddMinutes(-1), Now.AddHours(1),
                "Ops"));
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-caller-state=\"authenticated\"", html, StringComparison.Ordinal);
        Assert.Contains("data-caller-issuer=\"callora.host\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAssignedProvider_PutsItsIdentityAndClaimsIntoTheDocument()
    {
        await using var app = await CreateAppAsync(
            SurfaceAuthentication.SurfaceIdentity,
            identityPluginId: PluginId,
            provider: StubSurfaceIdentityProvider.Returning(PluginId, HostSurfaceIdentityResult.Identified(
                "crm.example", "lead-42", "password", Now.AddMinutes(-1), Now.AddHours(1), "Erika Muster",
                new Dictionary<string, IReadOnlyList<string>> { ["crm.roles"] = ["agent"] })));
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-caller-issuer=\"crm.example\"", html, StringComparison.Ordinal);
        Assert.Contains("data-caller-subject=\"lead-42\"", html, StringComparison.Ordinal);
        Assert.Contains("crm.roles", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAssignedProviderThatRecognisesNobody_RefusesAnAuthenticatedSurface()
    {
        await using var app = await CreateAppAsync(
            SurfaceAuthentication.SurfaceIdentity,
            identityPluginId: PluginId,
            provider: StubSurfaceIdentityProvider.Returning(PluginId, HostSurfaceIdentityResult.Anonymous));
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        // Not a redirect to the host login: the plugin owns that flow, and the host
        // has no business sending a portal visitor to the operator's sign-in.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnUnavailableProvider_ClosesAnAuthenticatedSurfaceInsteadOfDegrading()
    {
        await using var app = await CreateAppAsync(
            SurfaceAuthentication.SurfaceIdentity,
            identityPluginId: PluginId,
            provider: StubSurfaceIdentityProvider.Returning(PluginId, HostSurfaceIdentityResult.Identified(
                "crm.example", "lead-42", "password", Now.AddMinutes(-1), Now.AddHours(1))),
            unavailablePluginIds: [PluginId],
            hostIdentity: HostSurfaceIdentityResult.Identified(
                SurfaceIdentityIssuers.Host, "operator-7", "backend-session", Now.AddMinutes(-1), Now.AddHours(1)));
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        // Explicitly not 200 via the backend principal: a bound provider that cannot
        // be consulted must not silently fall through to a different identity.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task APublicSurfaceWithABrokenProvider_StillServesAnonymously()
    {
        await using var app = await CreateAppAsync(
            SurfaceAuthentication.Public,
            identityPluginId: PluginId,
            provider: StubSurfaceIdentityProvider.Throwing(PluginId));
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "data-caller-state=\"guest\"",
            await response.Content.ReadAsStringAsync(),
            StringComparison.Ordinal);
    }

    private static string CookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("; ", values)
            : string.Empty;

    private static string ExtractCookie(HttpResponseMessage response) =>
        CookieHeader(response).Split(';')[0];

    private static string SubjectOf(string html)
    {
        const string marker = "data-caller-subject=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return html[start..html.IndexOf('"', start)];
    }

    private static async Task<WebApplication> CreateAppAsync(
        SurfaceAuthentication authentication,
        string? identityPluginId = null,
        StubSurfaceIdentityProvider? provider = null,
        string[]? unavailablePluginIds = null,
        HostSurfaceIdentityResult? hostIdentity = null)
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        _ = await store.UpsertAsync(
            "tenant-a", "acme", "Acme", "spa", isActive: true, defaultSurfaceBaseUrl: "https://acme.example.de");
        store.SetSurface(
            "acme",
            authentication,
            identityPluginId: identityPluginId,
            identityAssignedAtUtc: identityPluginId is null ? null : Now.AddDays(-1));

        var catalog = new StaticPluginExportCatalog();
        if (provider is not null)
        {
            catalog.Add(PluginId, provider);
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(store);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(new InMemoryWorkspaceSurfaceStore());
        builder.Services.AddSingleton(new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a",
            AdminShellBaseUrl = "/admin",
            WorkspaceShellBaseUrl = "/",
        });
        builder.Services.AddSingleton<IPluginAvailabilityEvaluator>(
            new StaticPluginAvailabilityEvaluator(unavailablePluginIds ?? []));
        builder.Services.AddSingleton<ICalloraPluginCatalog>(catalog);
        builder.Services.AddCalloraSurfaceRendering();

        // The identity subsystem as the host composes it, with the transport pieces
        // swapped for in-memory doubles.
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton(new SurfaceIdentityOptions());
        builder.Services.AddSingleton<ISurfaceSessionCookieCodec, JsonSurfaceSessionCookieCodec>();
        builder.Services.AddSingleton<ISurfaceSessionStore, InMemorySurfaceSessionStore>();
        builder.Services.AddSingleton<Callora.Core.Application.Events.Contracts.IBusinessEventBus>(
            new RecordingBusinessEventBus());
        builder.Services.AddSingleton<ISurfaceHostIdentitySource>(
            new StubSurfaceHostIdentitySource(hostIdentity ?? HostSurfaceIdentityResult.Anonymous));
        builder.Services.AddScoped<ISurfaceCredentialReader, HttpContextSurfaceCredentialReader>();
        builder.Services.AddSingleton<SurfaceSessionCookieAccessor>();
        builder.Services.AddScoped<SurfaceIdentityResolver>();
        builder.Services.AddScoped<SurfaceSessionService>();
        builder.Services.AddScoped<SurfaceRequestCallerResolver>();
        builder.Services.AddSingleton(NullLoggerFactory.Instance);

        var app = builder.Build();
        app.MapSurfaceRenderEndpoints();
        await app.StartAsync();
        return app;
    }
}
