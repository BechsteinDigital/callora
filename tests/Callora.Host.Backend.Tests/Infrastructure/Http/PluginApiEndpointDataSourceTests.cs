using System.Net;
using System.Net.Http.Json;
using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Infrastructure.Http;
using Callora.Host.PluginContracts.Application.Http;
using Callora.Host.Backend.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Host.Backend.Tests.Infrastructure.Http;

public sealed class PluginApiEndpointDataSourceTests
{
    [Fact]
    public async Task PluginRoutes_EnforcePermissionAndDispatchActions()
    {
        await using var app = await CreateAppAsync();

        var noPermission = app.GetTestClient();
        var forbidden = await noPermission.GetAsync("/api/test-plugin/ping");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("application/problem+json", forbidden.Content.Headers.ContentType!.MediaType);

        var allowed = app.GetTestClient();
        allowed.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");
        var ok = await allowed.GetAsync("/api/test-plugin/ping");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var payload = await ok.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.True(payload!["pong"]);

        var writer = app.GetTestClient();
        writer.DefaultRequestHeaders.Add("X-Test-Permissions", "test.write");
        var created = await writer.PostAsJsonAsync("/api/test-plugin/echo", new { name = "acme" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/test-plugin/echo/acme", created.Headers.Location!.ToString());

        var invalid = await writer.PostAsJsonAsync("/api/test-plugin/echo", new { });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task WorkspaceRoutes_EnforceWorkspaceScope()
    {
        await using var app = await CreateAppAsync();

        // Workspace-gebundene Session: eigener Workspace erlaubt, fremder nicht.
        var boundClient = app.GetTestClient();
        boundClient.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");
        boundClient.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var own = await boundClient.GetAsync("/api/test-plugin/items?workspaceKey=workspace-a");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);

        var foreign = await boundClient.GetAsync("/api/test-plugin/items?workspaceKey=workspace-b");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }

    [Fact]
    public async Task Refresh_RemovesRoutesWhenExportsVanish()
    {
        var catalog = new MutablePluginCatalog();
        catalog.SetExports(new TestPluginAdminController());
        var dataSource = new PluginApiEndpointDataSource(
            catalog,
            NullLogger<PluginApiEndpointDataSource>.Instance);
        dataSource.Refresh();
        await using var app = await CreateAppAsync(dataSource);

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/test-plugin/ping")).StatusCode);

        catalog.SetExports();
        dataSource.Refresh();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/test-plugin/ping")).StatusCode);
    }

    [Fact]
    public async Task ReservedRoute_IsRejected_AndNotMapped()
    {
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IApiController)] = [new HijackingPluginController()]
        });
        var dataSource = new PluginApiEndpointDataSource(
            catalog,
            NullLogger<PluginApiEndpointDataSource>.Instance);
        dataSource.Refresh();
        await using var app = await CreateAppAsync(dataSource);

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");

        // The hijack route was refused during refresh, so nothing answers here.
        var response = await client.PostAsJsonAsync("/api/auth/login", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DuplicatePluginRoute_IsRejected_FirstWins()
    {
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            // Two controllers claim the same method+route (as two plugins would).
            [typeof(IApiController)] = [new TestPluginAdminController(), new TestPluginAdminController()]
        });
        var dataSource = new PluginApiEndpointDataSource(
            catalog,
            NullLogger<PluginApiEndpointDataSource>.Instance);
        dataSource.Refresh();
        await using var app = await CreateAppAsync(dataSource);

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");

        // Without the first-wins guard the duplicate would throw
        // AmbiguousMatchException (500) at request time.
        var response = await client.GetAsync("/api/test-plugin/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FaultingAction_ReturnsStructuredServerError()
    {
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IApiController)] = [new FaultingPluginController()]
        });
        var dataSource = new PluginApiEndpointDataSource(
            catalog,
            NullLogger<PluginApiEndpointDataSource>.Instance);
        dataSource.Refresh();
        await using var app = await CreateAppAsync(dataSource);

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");

        var response = await client.GetAsync("/api/test-plugin/boom");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task WorkspaceRoute_ToUnavailablePlugin_IsForbidden()
    {
        // The caller is correctly workspace-scoped and permitted, but the plugin
        // is not effectively available in the workspace (REV2 §13) → 403.
        await using var app = await CreateAppAsync(
            availability: new StaticPluginAvailabilityEvaluator("test-plugin"));

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var response = await client.GetAsync("/api/test-plugin/items?workspaceKey=workspace-a");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task WorkspaceRoute_WithoutAvailabilityEvaluator_IsServed()
    {
        // Contract: the availability gate is an additional layer on top of the
        // always-enforced auth/permission/workspace-scope checks. When no
        // evaluator is registered it fails open, so a host that has not wired
        // availability still serves its workspace routes.
        await using var app = await CreateAppAsync(registerAvailability: false);

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "test.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var response = await client.GetAsync("/api/test-plugin/items?workspaceKey=workspace-a");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync(
        PluginApiEndpointDataSource? dataSource = null,
        IPluginAvailabilityEvaluator? availability = null,
        bool registerAvailability = true)
    {
        if (dataSource is null)
        {
            var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
            {
                [typeof(IApiController)] =
                [
                    new TestPluginAdminController(),
                    new TestPluginWorkspaceController()
                ]
            });
            dataSource = new PluginApiEndpointDataSource(
                catalog,
                NullLogger<PluginApiEndpointDataSource>.Instance);
            dataSource.Refresh();
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        if (registerAvailability)
        {
            builder.Services.AddSingleton<IPluginAvailabilityEvaluator>(
                availability ?? new StaticPluginAvailabilityEvaluator());
        }

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ((IEndpointRouteBuilder)app).DataSources.Add(dataSource);
        await app.StartAsync();
        return app;
    }
}
