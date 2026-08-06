using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Extensions;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Callora.Core.Tests.Api;

/// <summary>The HTTP surface of per-surface theming.</summary>
public sealed class SurfaceThemeEndpointsTests
{
    private const string Workspace = "workspace-a";
    private const string Surface = "shop";
    private const string WorkspaceTheme = "theme-alpha";
    private const string SurfaceTheme = "theme-beta";
    private const string Version = "1.0.0";
    private const string BasePath = $"/api/themes/workspaces/{Workspace}/surfaces/{Surface}";

    [Fact]
    public async Task Get_ReportsTheInheritedWorkspaceTheme()
    {
        await using var app = await CreateAppAsync();

        var assignment = await Client(app).GetFromJsonAsync<SurfaceThemeAssignmentApiResponse>(BasePath);

        Assert.NotNull(assignment);
        Assert.True(assignment!.InheritedFromWorkspace);
        Assert.Equal(WorkspaceTheme, assignment.ThemePluginId);
    }

    [Fact]
    public async Task Put_AssignsAThemeToTheSurface()
    {
        await using var app = await CreateAppAsync();

        var response = await Client(app).PutAsJsonAsync(
            BasePath,
            new SurfaceThemeAssignmentUpsertApiRequest(SurfaceTheme, Version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignment = await response.Content.ReadFromJsonAsync<SurfaceThemeAssignmentApiResponse>();
        Assert.False(assignment!.InheritedFromWorkspace);
        Assert.Equal(SurfaceTheme, assignment.ThemePluginId);
    }

    [Fact]
    public async Task Put_RejectsAnUnregisteredTheme()
    {
        await using var app = await CreateAppAsync();

        var response = await Client(app).PutAsJsonAsync(
            BasePath,
            new SurfaceThemeAssignmentUpsertApiRequest("theme-unknown", Version));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ReturnsTheSurfaceToItsWorkspaceTheme()
    {
        await using var app = await CreateAppAsync();
        var client = Client(app);
        _ = await client.PutAsJsonAsync(BasePath, new SurfaceThemeAssignmentUpsertApiRequest(SurfaceTheme, Version));

        var response = await client.DeleteAsync(BasePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assignment = await response.Content.ReadFromJsonAsync<SurfaceThemeAssignmentApiResponse>();
        Assert.True(assignment!.InheritedFromWorkspace);
        Assert.Equal(WorkspaceTheme, assignment.ThemePluginId);
    }

    [Fact]
    public async Task Settings_SeparateOwnValuesFromInheritedOnes()
    {
        // The workspace level is seeded through the store; its own endpoint has
        // its own test, and mapping it here would drag in the whole template
        // resolution graph.
        await using var app = await CreateAppAsync(seedWorkspaceValue: true);
        var client = Client(app);

        _ = await client.PutAsJsonAsync(
            $"{BasePath}/settings",
            new UpsertWorkspaceThemeSettingsApiRequest(Values(("logo.text", "\"Shop\""))));

        var settings = await client.GetFromJsonAsync<SurfaceThemeSettingsApiResponse>($"{BasePath}/settings");
        Assert.True(settings!.InheritsWorkspaceValues);
        Assert.Equal("\"#336699\"", settings.InheritedValuesByKey["primary.color"]);
        Assert.Equal("\"Shop\"", settings.ValuesByKey["logo.text"]);
        Assert.False(settings.ValuesByKey.ContainsKey("primary.color"));
    }

    [Fact]
    public async Task Settings_ForAnUnknownSurface_Are404()
    {
        await using var app = await CreateAppAsync();

        var response = await Client(app)
            .GetAsync($"/api/themes/workspaces/{Workspace}/surfaces/nope/settings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_RequiresTheUpdatePermission()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.read");

        var response = await client.PutAsJsonAsync(
            BasePath,
            new SurfaceThemeAssignmentUpsertApiRequest(SurfaceTheme, Version));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpClient Client(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "extension.read,extension.update");
        return client;
    }

    private static Dictionary<string, JsonElement> Values(params (string Key, string Json)[] values) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => JsonSerializer.Deserialize<JsonElement>(pair.Json),
            StringComparer.OrdinalIgnoreCase);

    private static async Task<WebApplication> CreateAppAsync(bool seedWorkspaceValue = false)
    {
        var workspaces = new InMemoryWorkspaceManagementStore();
        workspaces.AddTenant("tenant-a");
        _ = await workspaces.UpsertAsync("tenant-a", Workspace, "Workspace A", "shop", true);
        _ = await workspaces.UpsertThemeAssignmentAsync(Workspace, WorkspaceTheme, Version, "tester");

        var surfaces = new InMemoryWorkspaceSurfaceStore();
        _ = await surfaces.UpsertAsync(
            Workspace,
            new WorkspaceSurfaceInput(
                Surface,
                "Shop",
                "spa",
                PublicBaseUrl: null,
                PublicHost: null,
                PublicPathPrefix: "/shop",
                AccessMode: SurfaceAccessMode.Public,
                Locale: "de",
                TemplatePluginId: null,
                TemplateVersion: null,
                ThemePluginId: null,
                ThemeVersion: null,
                IsActive: true));

        var templates = new InMemoryWorkspaceTemplateRegistryStore();
        var settings = new InMemoryWorkspaceThemeSettingsStore();
        foreach (var pluginId in new[] { WorkspaceTheme, SurfaceTheme })
        {
            _ = await templates.UpsertDefinitionAsync(
                $"workspace.{pluginId}",
                "surface",
                pluginId,
                Version,
                pluginId,
                $"themes/{pluginId}.html",
                parentTemplateKey: null,
                scope: "workspace",
                isActive: true,
                priority: 100);
            _ = await settings.ReplaceDefinitionsForPluginAsync(
                pluginId,
                Version,
                [
                    new("primary.color", "Primärfarbe", "color", null, "\"#000000\"", false, 0, null, null, true),
                    new("logo.text", "Logo", "text", null, "\"Default\"", false, 1, null, null, true),
                ]);
        }

        if (seedWorkspaceValue)
        {
            _ = await settings.ReplaceValuesAsync(
                Workspace,
                surfaceKey: null,
                WorkspaceTheme,
                new Dictionary<string, string?> { ["primary.color"] = "\"#336699\"" });
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton(new BackendHostOptions { DefaultTenantKey = "tenant-a" });
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaces);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(surfaces);
        builder.Services.AddSingleton<IWorkspaceTemplateRegistryStore>(templates);
        builder.Services.AddSingleton<IWorkspaceThemeSettingsStore>(settings);
        builder.Services.AddSingleton<SurfaceThemeService>();
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionCache, NoOpWorkspaceTemplateResolutionCache>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSurfaceThemeEndpoints();
        await app.StartAsync();
        return app;
    }
}
