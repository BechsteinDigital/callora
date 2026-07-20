using Callora.Core.Application.Extensions;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Surface;

public sealed class SurfaceRenderEndpointsTests
{
    [Fact]
    public async Task Render_ResolvesWorkspaceByHost_ReturnsRenderedHtml()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"callora-app\"", html, StringComparison.Ordinal);
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_UnknownHost_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://unknown.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Render_WithAssignedTheme_FlowsEffectiveTokensIntoContext()
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        _ = await store.UpsertAsync("tenant-a", "acme", "Acme", "spa", isActive: true, publicBaseUrl: "https://acme.example.de");
        _ = await store.UpsertThemeAssignmentAsync("acme", "acme.brand-theme", "1.0.0", assignedBy: "op");

        var settings = new InMemoryWorkspaceThemeSettingsStore();
        _ = await settings.ReplaceDefinitionsForPluginAsync(
            "acme.brand-theme",
            "1.0.0",
            [
                new WorkspaceThemeSettingDefinitionInput(
                    SettingKey: "primaryColor",
                    Label: "Primary Color",
                    FieldType: "color",
                    Description: null,
                    DefaultValueJson: "\"#007bff\"",
                    IsRequired: false,
                    SortOrder: 10,
                    GroupName: null,
                    OptionsJson: null,
                    IsActive: true),
                // A secret-typed setting must never reach the anonymous surface context.
                new WorkspaceThemeSettingDefinitionInput(
                    SettingKey: "apiSecret",
                    Label: "API Secret",
                    FieldType: "secret",
                    Description: null,
                    DefaultValueJson: "\"do-not-leak\"",
                    IsRequired: false,
                    SortOrder: 20,
                    GroupName: null,
                    OptionsJson: null,
                    IsActive: true),
            ]);
        _ = await settings.ReplaceWorkspaceValuesAsync(
            "acme",
            "acme.brand-theme",
            new Dictionary<string, string?> { ["primaryColor"] = "#e4002b" });

        var capturingRenderer = new CapturingSurfaceRenderer();

        await using var app = await CreateAppAsync(
            store,
            configure: services =>
            {
                services.AddSingleton<IWorkspaceThemeSettingsStore>(settings);
                services.AddScoped<WorkspacePublicThemeResolver>();
                services.AddSingleton<ISurfaceRenderer>(capturingRenderer);
            });
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = capturingRenderer.LastContext!.Tokens;
        // The workspace override wins over the definition default.
        Assert.Equal("#e4002b", tokens["primaryColor"]);
        Assert.Equal("acme.brand-theme", tokens[SurfaceThemeTokens.ThemePluginIdKey]);
        Assert.Equal("1.0.0", tokens[SurfaceThemeTokens.ThemeVersionKey]);
        Assert.False(tokens.ContainsKey("apiSecret"));
    }

    [Fact]
    public async Task Render_AuthenticatedPolicy_AnonymousCaller_RedirectsToLogin()
    {
        var store = await SeededStoreAsync();
        _ = await store.SetSurfaceAccessPolicyAsync("acme", SurfaceAccessPolicy.Authenticated);

        await using var app = await CreateAppAsync(store, configure: null);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("/login", location, StringComparison.Ordinal);
        Assert.Contains("workspaceKey=acme", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_AuthenticatedPolicy_AuthenticatedCaller_Renders()
    {
        var store = await SeededStoreAsync();
        _ = await store.SetSurfaceAccessPolicyAsync("acme", SurfaceAccessPolicy.Authenticated);

        await using var app = await CreateAppAsync(store, configure: null, authenticate: true);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    private static async Task<InMemoryWorkspaceManagementStore> SeededStoreAsync()
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        _ = await store.UpsertAsync("tenant-a", "acme", "Acme", "spa", isActive: true, publicBaseUrl: "https://acme.example.de");
        return store;
    }

    private static Task<WebApplication> CreateAppAsync() => CreateAppAsync(null, null);

    private static async Task<WebApplication> CreateAppAsync(
        InMemoryWorkspaceManagementStore? store,
        Action<IServiceCollection>? configure,
        bool authenticate = false)
    {
        store ??= await SeededStoreAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(store);
        builder.Services.AddCalloraSurfaceRendering();
        if (authenticate)
        {
            builder.Services
                .AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            builder.Services.AddAuthorization();
        }
        // A capturing/overriding renderer, the theme resolver etc. are layered on last so
        // the caller's registrations win over the defaults.
        configure?.Invoke(builder.Services);

        var app = builder.Build();
        if (authenticate)
        {
            app.UseAuthentication();
        }
        app.MapSurfaceRenderEndpoints();
        await app.StartAsync();
        return app;
    }

    private sealed class CapturingSurfaceRenderer : ISurfaceRenderer
    {
        public SurfaceRenderContext? LastContext { get; private set; }

        public string Render(string templateText, SurfaceRenderContext context)
        {
            LastContext = context;
            return "<html><!-- captured --></html>";
        }

        public string Render(string templateText, SurfaceRenderContext context, IReadOnlyList<string> bundleChain)
        {
            LastContext = context;
            return "<html><!-- captured --></html>";
        }
    }
}
