using System.Net;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Tests.Support;
using Callora.Host.Workspace.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Api;

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
    public async Task ThemeEndpoint_WithoutAssignedTheme_ReturnsEmptyValues()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/workspace/public/theme?workspaceKey=workspace-public");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"valuesByKey\":{}", content, StringComparison.Ordinal);
    }

    private static async Task<WebApplication> CreateAppAsync()
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
        builder.Services.AddSingleton(new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a",
            AdminShellBaseUrl = "https://admin-shell.local/admin/",
            WorkspaceShellBaseUrl = "https://workspace-shell.local/"
        });
        builder.Services.AddSingleton<Callora.Host.Backend.Application.Extensions.IWorkspaceTemplateResolutionService>(
            new StaticWorkspaceTemplateResolutionService([]));
        builder.Services.AddSingleton<Callora.Host.Backend.Application.Plugins.IWorkspacePluginActivationReader>(
            new StaticWorkspacePluginActivationReader(["dialer", "voip"]));
        builder.Services.AddSingleton<Callora.Host.Backend.Application.Extensions.IWorkspaceThemeSettingsStore>(
            new InMemoryWorkspaceThemeSettingsStore());
        builder.Services.AddScoped<Callora.Host.Backend.Application.Extensions.WorkspaceUiChainResolver>();
        builder.Services.AddScoped<Callora.Host.Backend.Application.Extensions.WorkspacePublicThemeResolver>();

        var app = builder.Build();
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
