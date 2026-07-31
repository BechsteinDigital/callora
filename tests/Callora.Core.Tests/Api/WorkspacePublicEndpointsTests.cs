using Callora.Administration.Api;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Callora.Workspace.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Callora.Core.Tests.Api;

public sealed class WorkspacePublicEndpointsTests
{
    [Fact]
    public async Task PublicRoute_WithConfiguredHostPath_RedirectsToWorkspaceShell()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/dialer");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://workspace-shell.local/dialer",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task SameOriginShell_NotFoundSinkPath_ReturnsTerminal404_NoRedirectLoop()
    {
        // A same-origin shell base ("/") is the production default: the not-found
        // redirect target ("/404") is served by this same app. Without a terminal
        // guard, a request to /404 stays unresolved and redirects to /404 forever.
        await using var app = await CreateAppAsync(workspaceShellBaseUrl: "/");

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/404");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SameOriginShell_UnknownRoute_RedirectsToNotFoundSinkOnce()
    {
        await using var app = await CreateAppAsync(workspaceShellBaseUrl: "/");

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/does-not-exist");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        // One hop to the sink, which is now terminal (asserted above) — not a loop.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/404", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task SameOriginShell_ResolvableWorkspacePath_RedirectsToAdminShell_NotLoop()
    {
        // The seeded workspace resolves at /dialer. With a same-origin shell base ("/") the shell
        // redirect target is /dialer again — a self-redirect loop. There is no separate workspace-shell
        // SPA to hand off to in a colocated deployment, so fall back to the admin shell.
        await using var app = await CreateAppAsync(workspaceShellBaseUrl: "/");

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/dialer");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://admin-shell.local/admin/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task SameOriginShell_RootWithResolvableWorkspace_RedirectsToAdminShell_NotLoop()
    {
        // Colocated deploy default: WorkspaceShellBaseUrl="/" and a workspace resolves at the root.
        // The root handler would redirect "/" → "/" forever; instead it sends the operator to the admin shell.
        await using var app = await CreateAppAsync(workspaceShellBaseUrl: "/");
        var store = (InMemoryWorkspaceManagementStore)app.Services.GetRequiredService<IWorkspaceManagementStore>();
        _ = await store.UpsertAsync(
            tenantKey: "tenant-a",
            workspaceKey: "workspace-root",
            displayName: "Workspace Root",
            workspaceType: "voice",
            isActive: true,
            publicBaseUrl: "localhost");

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://admin-shell.local/admin/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task SameOriginShell_LoginForRootWorkspace_RedirectsToAdminShell_NotLoop()
    {
        // /login for a root-prefix workspace on a same-origin shell base ("/") would redirect to
        // /login forever. The self-redirect guard sends it to the admin shell instead.
        await using var app = await CreateAppAsync(workspaceShellBaseUrl: "/");
        var store = (InMemoryWorkspaceManagementStore)app.Services.GetRequiredService<IWorkspaceManagementStore>();
        _ = await store.UpsertAsync(
            tenantKey: "tenant-a",
            workspaceKey: "workspace-root",
            displayName: "Workspace Root",
            workspaceType: "voice",
            isActive: true,
            publicBaseUrl: "localhost");

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/login");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://admin-shell.local/admin/", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task WorkspaceBootstrapScript_UsesRefererPathForContextResolution()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/workspace/public/bootstrap.js");
        request.Headers.Host = "localhost";
        request.Headers.Referrer = new Uri("http://localhost/dialer");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("workspace-public", content, StringComparison.Ordinal);
        Assert.Contains("/dialer", content, StringComparison.Ordinal);
        Assert.Equal("application/javascript; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task WorkspaceBootstrapScript_WithUnknownRoute_ReturnsDefaultContext()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/workspace/public/bootstrap.js");
        request.Headers.Host = "localhost";
        request.Headers.Referrer = new Uri("http://localhost/unknown");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"key\":\"default\"", content, StringComparison.Ordinal);
        Assert.Contains("\"publicPathPrefix\":\"/unknown\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveEndpoint_WithUnknownRoute_ReturnsResolvedFalse()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/workspace/public/resolve");
        request.Headers.Host = "localhost";
        request.Headers.Add("X-Forwarded-Host", "localhost");
        request.Headers.Add("X-Forwarded-Uri", "/unknown");

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"resolved\":false", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginRoute_RedirectsToWorkspaceShellLoginWithWorkspaceContext()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/login?returnUrl=%2Fdialer");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.Equal("https://workspace-shell.local/dialer/login", location!.GetLeftPart(UriPartial.Path));

        var query = ParseQueryString(location.Query);
        Assert.Equal("/dialer", query["returnUrl"]);
        Assert.False(query.ContainsKey("workspaceKey"));
    }

    [Fact]
    public async Task AdminRoute_RedirectsToConfiguredAdminShell()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/admin/users?tab=roles");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://admin-shell.local/admin/users?tab=roles",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task ReservedApiPath_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnknownPublicRoute_RedirectsToWorkspaceShellNotFoundPage()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/unknown");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://workspace-shell.local/404",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task LoginRoute_WithUnknownWorkspaceReturnUrl_RedirectsToWorkspaceShellNotFoundPage()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/login?returnUrl=%2F");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://workspace-shell.local/404",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task RootRoute_WithoutWorkspaceMapping_RedirectsToWorkspaceShellNotFoundPage()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Host = "localhost";

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "https://workspace-shell.local/404",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task UiChainEndpoint_ReturnsOrderedPluginChain()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/workspace/public/ui-chain?workspaceKey=workspace-public");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"chain\":[\"dialer\",\"voip\"]", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UiChainEndpoint_WithSurfaceTemplate_ReturnsSurfaceSpecificChain()
    {
        await using var app = await CreateAppAsync();
        await SeedSurfaceAsync(
            app,
            "workspace-public",
            "videoconference",
            SurfaceAccessMode.Mixed,
            templatePluginId: "videoconference");

        var client = app.GetTestClient();
        var response = await client.GetAsync(
            "/workspace/public/ui-chain?workspaceKey=workspace-public&surfaceKey=videoconference");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            "\"chain\":[\"videoconference\",\"dialer\",\"voip\"]",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeEndpoint_WithoutAssignedTheme_ReturnsEmptyValues()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/workspace/public/theme?workspaceKey=workspace-public");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"valuesByKey\":{}", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UiChain_AuthenticatedWorkspace_AnonymousCaller_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync();
        var store = (InMemoryWorkspaceManagementStore)app.Services.GetRequiredService<IWorkspaceManagementStore>();
        _ = await store.SetSurfaceAccessPolicyAsync("workspace-public", SurfaceAccessPolicy.Authenticated);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/workspace/public/ui-chain?workspaceKey=workspace-public");

        // Indistinguishable from a non-existent workspace — no inventory leak.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UiChain_AuthenticatedWorkspace_AuthenticatedCaller_ReturnsChain()
    {
        await using var app = await CreateAppAsync(authenticate: true);
        var store = (InMemoryWorkspaceManagementStore)app.Services.GetRequiredService<IWorkspaceManagementStore>();
        _ = await store.SetSurfaceAccessPolicyAsync("workspace-public", SurfaceAccessPolicy.Authenticated);

        var client = app.GetTestClient();
        var response = await client.GetAsync("/workspace/public/ui-chain?workspaceKey=workspace-public");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"chain\":[\"dialer\",\"voip\"]", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UiChain_PublicSurface_AnonymousCaller_InAuthenticatedWorkspace_ReturnsChain()
    {
        // The workspace is Authenticated but the named surface is Public: the per-surface
        // gate must let the anonymous caller through, matching /surface/render.
        await using var app = await CreateAppAsync();
        var store = (InMemoryWorkspaceManagementStore)app.Services.GetRequiredService<IWorkspaceManagementStore>();
        _ = await store.SetSurfaceAccessPolicyAsync("workspace-public", SurfaceAccessPolicy.Authenticated);
        await SeedSurfaceAsync(app, "workspace-public", "site", SurfaceAccessMode.Public);

        var client = app.GetTestClient();
        var response = await client.GetAsync(
            "/workspace/public/ui-chain?workspaceKey=workspace-public&surfaceKey=site");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"chain\":[\"dialer\",\"voip\"]", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UiChain_MixedSurface_AnonymousCaller_InAuthenticatedWorkspace_ReturnsChain()
    {
        // A Mixed surface has public routes, so its chain must load anonymously.
        await using var app = await CreateAppAsync();
        var store = (InMemoryWorkspaceManagementStore)app.Services.GetRequiredService<IWorkspaceManagementStore>();
        _ = await store.SetSurfaceAccessPolicyAsync("workspace-public", SurfaceAccessPolicy.Authenticated);
        await SeedSurfaceAsync(app, "workspace-public", "shop", SurfaceAccessMode.Mixed);

        var client = app.GetTestClient();
        var response = await client.GetAsync(
            "/workspace/public/ui-chain?workspaceKey=workspace-public&surfaceKey=shop");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"chain\":[\"dialer\",\"voip\"]", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UiChain_AuthenticatedSurface_AnonymousCaller_ReturnsNotFound()
    {
        // The workspace is Public but the named surface is Authenticated: the per-surface
        // gate must 404 the anonymous caller (no inventory leak).
        await using var app = await CreateAppAsync();
        await SeedSurfaceAsync(app, "workspace-public", "desk", SurfaceAccessMode.Authenticated);

        var client = app.GetTestClient();
        var response = await client.GetAsync(
            "/workspace/public/ui-chain?workspaceKey=workspace-public&surfaceKey=desk");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UiChain_AuthenticatedSurface_AuthenticatedCaller_ReturnsChain()
    {
        await using var app = await CreateAppAsync(authenticate: true);
        await SeedSurfaceAsync(app, "workspace-public", "desk", SurfaceAccessMode.Authenticated);

        var client = app.GetTestClient();
        var response = await client.GetAsync(
            "/workspace/public/ui-chain?workspaceKey=workspace-public&surfaceKey=desk");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"chain\":[\"dialer\",\"voip\"]", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UiChain_UnknownSurfaceKey_FallsBackToWorkspaceGate()
    {
        // The surface does not exist, so the gate falls back to the workspace-wide policy:
        // an Authenticated workspace 404s the anonymous caller even with a surfaceKey.
        await using var app = await CreateAppAsync();
        var store = (InMemoryWorkspaceManagementStore)app.Services.GetRequiredService<IWorkspaceManagementStore>();
        _ = await store.SetSurfaceAccessPolicyAsync("workspace-public", SurfaceAccessPolicy.Authenticated);

        var client = app.GetTestClient();
        var response = await client.GetAsync(
            "/workspace/public/ui-chain?workspaceKey=workspace-public&surfaceKey=missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task SeedSurfaceAsync(
        WebApplication app,
        string workspaceKey,
        string surfaceKey,
        SurfaceAccessMode accessMode,
        string? templatePluginId = null)
    {
        var surfaceStore = app.Services.GetRequiredService<IWorkspaceSurfaceStore>();
        _ = await surfaceStore.UpsertAsync(
            workspaceKey,
            new WorkspaceSurfaceInput(
                SurfaceKey: surfaceKey,
                DisplayName: surfaceKey,
                SurfaceType: "web",
                PublicBaseUrl: null,
                PublicHost: null,
                PublicPathPrefix: "/",
                AccessMode: accessMode,
                Locale: null,
                TemplatePluginId: templatePluginId,
                TemplateVersion: null,
                ThemePluginId: null,
                ThemeVersion: null,
                IsActive: true));
    }

    private static async Task<WebApplication> CreateAppAsync(
        bool authenticate = false,
        string workspaceShellBaseUrl = "https://workspace-shell.local/")
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        _ = await workspaceStore.UpsertAsync(
            tenantKey: "tenant-a",
            workspaceKey: "workspace-public",
            displayName: "Workspace Public",
            workspaceType: "voice",
            isActive: true,
            publicBaseUrl: "localhost/dialer");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaceStore);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(new InMemoryWorkspaceSurfaceStore());
        builder.Services.AddSingleton(new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a",
            AdminShellBaseUrl = "https://admin-shell.local/admin/",
            WorkspaceShellBaseUrl = workspaceShellBaseUrl
        });
        builder.Services.AddSingleton<Callora.Core.Application.Extensions.IWorkspaceTemplateResolutionService>(
            new StaticWorkspaceTemplateResolutionService([]));
        builder.Services.AddSingleton<Callora.Core.Application.Plugins.IWorkspacePluginActivationReader>(
            new StaticWorkspacePluginActivationReader(["dialer", "voip"]));
        builder.Services.AddSingleton<Callora.Core.Application.Plugins.IPluginAvailabilityEvaluator>(
            new StaticPluginAvailabilityEvaluator());
        builder.Services.AddSingleton<Callora.Core.Application.Extensions.IWorkspaceThemeSettingsStore>(
            new InMemoryWorkspaceThemeSettingsStore());
        builder.Services.AddScoped<Callora.Core.Application.Extensions.WorkspaceUiChainResolver>();
        builder.Services.AddScoped<Callora.Core.Application.Extensions.WorkspacePublicThemeResolver>();
        if (authenticate)
        {
            builder.Services
                .AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            builder.Services.AddAuthorization();
        }

        var app = builder.Build();
        if (authenticate)
        {
            app.UseAuthentication();
        }
        app.MapWorkspacePublicEndpoints();
        await app.StartAsync();
        return app;
    }

    private static Dictionary<string, string?> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var normalized = query.Trim().TrimStart('?');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return result;
        }

        var pairs = normalized.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex < 0)
            {
                result[Uri.UnescapeDataString(pair)] = string.Empty;
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separatorIndex]);
            var value = Uri.UnescapeDataString(pair[(separatorIndex + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
