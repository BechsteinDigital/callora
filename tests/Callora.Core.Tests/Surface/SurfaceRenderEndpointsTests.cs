using Callora.Core.Application.Extensions;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Api;
using Callora.Workspace.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Surface;

[Collection(SurfaceRenderingCollection.Name)]
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
    public async Task ColocatedSurfaceRoute_WithWorkspaceEndpointsMapped_RendersWithoutRedirect()
    {
        await using var app = await CreateAppAsync(
            store: null,
            configure: null,
            mapWorkspaceEndpoints: true);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        Assert.Null(response.Headers.Location);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnApplicationSurface_ServesItsOwnSubPaths()
    {
        // Der Fall, für den die Achse existiert: Eine Raumverwaltung liefert `/raeume/abc123`
        // aus, obwohl es keinen Knoten dafür gibt — der Raum entsteht zur Laufzeit und kann gar
        // nicht als Seite angelegt worden sein.
        //
        // Der Renderweg bleibt derselbe: dieselbe Shell, dasselbe Theme. Genau daran hängt
        // White-Label; eine Anwendung, die ihre eigene Optik mitbrächte, fiele auf.
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.Public, routing: SurfaceRouting.Application);

        await using var app = await CreateAppAsync(store, configure: null);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/raeume/abc123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ATreeSurface_RefusesAPathThatIsNotANode()
    {
        // Dieselbe Anfrage an dieselbe Fläche — nur die Adressierung unterscheidet sich. Ohne
        // diese Gegenprobe belegte der Test oben nur, dass irgendetwas 200 antwortet.
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.Public, routing: SurfaceRouting.Tree);

        await using var app = await CreateAppAsync(store, configure: null);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/raeume/abc123");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ColocatedSurfaceRoute_ForAPathNobodyServes_Is404AndStillNoRedirect()
    {
        // Zwei Aussagen in einer: Der Pfad gehört keinem Knoten, also 404 — und trotzdem
        // gewinnt die Surface-Route gegen den Workspace-Catch-All, der hierauf mit einer
        // Umleitung zur Admin-Shell geantwortet hätte.
        //
        // Vor der Restpfad-Prüfung kam hier 200 mit der Wurzelseite: Die Auflösung nimmt das
        // längste passende Präfix, und `/meet` fiel hinter `/` unter den Tisch.
        await using var app = await CreateAppAsync(
            store: null,
            configure: null,
            mapWorkspaceEndpoints: true);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/meet");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.Location);
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
        _ = await store.UpsertAsync("tenant-a", "acme", "Acme", "spa", isActive: true, defaultSurfaceBaseUrl: "https://acme.example.de");
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
        _ = await settings.ReplaceValuesAsync(
            "acme",
            surfaceKey: null,
            "acme.brand-theme",
            new Dictionary<string, string?> { ["primaryColor"] = "#e4002b" });

        var capturingRenderer = new CapturingSurfaceRenderer();

        await using var app = await CreateAppAsync(
            store,
            configure: services =>
            {
                services.AddSingleton<IWorkspaceThemeSettingsStore>(settings);
                services.AddSingleton<IWorkspaceSectionLayoutStore>(
                    new InMemoryWorkspaceSectionLayoutStore());
                services.AddScoped<WorkspacePublicThemeResolver>();
                services.AddScoped<IWorkspacePublicThemeResolver>(
                    static sp => sp.GetRequiredService<WorkspacePublicThemeResolver>());
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
    public async Task Render_PublicSurface_AnonymousCaller_Renders()
    {
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.Public);

        await using var app = await CreateAppAsync(store, configure: null);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_AuthenticatedSurface_AnonymousCaller_RedirectsToLogin()
    {
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.SurfaceIdentity);

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
    public async Task Render_MixedSurface_AnonymousCaller_RendersShell()
    {
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.Public);

        await using var app = await CreateAppAsync(store, configure: null);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_AuthenticatedSurface_AuthenticatedCaller_Renders()
    {
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.SurfaceIdentity);

        await using var app = await CreateAppAsync(store, configure: null, authenticate: true);
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_FlowsPerSurfaceKeyAndLocale_IntoContext()
    {
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.Public, surfaceKey: "partner", locale: "en");

        var capturingRenderer = new CapturingSurfaceRenderer();
        await using var app = await CreateAppAsync(
            store,
            configure: services => services.AddSingleton<ISurfaceRenderer>(capturingRenderer));
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var context = capturingRenderer.LastContext!;
        Assert.Equal("partner", context.SurfaceKey);
        Assert.Equal("en", context.Locale);
        Assert.Equal("acme", context.WorkspaceKey);
        Assert.Equal("tenant-a", context.TenantKey);
    }

    [Fact]
    public async Task Render_SurfaceWithoutLocale_DefaultsLocaleToDe()
    {
        var store = await SeededStoreAsync();
        store.SetSurface("acme", SurfaceAuthentication.Public, surfaceKey: "default", locale: null);

        var capturingRenderer = new CapturingSurfaceRenderer();
        await using var app = await CreateAppAsync(
            store,
            configure: services => services.AddSingleton<ISurfaceRenderer>(capturingRenderer));
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("de", capturingRenderer.LastContext!.Locale);
    }

    private static async Task<InMemoryWorkspaceManagementStore> SeededStoreAsync()
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        _ = await store.UpsertAsync("tenant-a", "acme", "Acme", "spa", isActive: true, defaultSurfaceBaseUrl: "https://acme.example.de");
        return store;
    }

    private static Task<WebApplication> CreateAppAsync() => CreateAppAsync(null, null);

    private static async Task<WebApplication> CreateAppAsync(
        InMemoryWorkspaceManagementStore? store,
        Action<IServiceCollection>? configure,
        bool authenticate = false,
        bool mapWorkspaceEndpoints = false)
    {
        store ??= await SeededStoreAsync();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(store);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(new InMemoryWorkspaceSurfaceStore());
        builder.Services.AddSingleton(new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a",
            AdminShellBaseUrl = "/admin",
            WorkspaceShellBaseUrl = "/"
        });
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionService>(
            new StaticWorkspaceTemplateResolutionService([]));
        builder.Services.AddSingleton<IWorkspacePluginActivationReader>(
            new StaticWorkspacePluginActivationReader([]));
        builder.Services.AddSingleton<IPluginAvailabilityEvaluator>(
            new StaticPluginAvailabilityEvaluator());
        builder.Services.AddSingleton<IWorkspaceThemeSettingsStore>(
            new InMemoryWorkspaceThemeSettingsStore());
        builder.Services.AddScoped<WorkspaceUiChainResolver>();
        builder.Services.AddSingleton<IWorkspaceSectionLayoutStore>(
            new InMemoryWorkspaceSectionLayoutStore());
        builder.Services.AddScoped<WorkspacePublicThemeResolver>();
        // Der Port zeigt hier direkt auf den echten Resolver, nicht auf den Cache: Diese Tests
        // schreiben und lesen im selben Lauf und müssen sehen, was sie gerade gesetzt haben.
        builder.Services.AddScoped<IWorkspacePublicThemeResolver>(
            static sp => sp.GetRequiredService<WorkspacePublicThemeResolver>());
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
        if (mapWorkspaceEndpoints)
        {
            app.MapWorkspacePublicEndpoints();
        }
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
