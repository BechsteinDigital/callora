using System.Net;
using System.Net.Http.Json;
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

    private static async Task<WebApplication> CreateAppAsync(PluginApiEndpointDataSource? dataSource = null)
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

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ((IEndpointRouteBuilder)app).DataSources.Add(dataSource);
        await app.StartAsync();
        return app;
    }
}
