using Callora.Administration.Api;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Policies;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Callora.Core.Tests.Api;

public sealed class EntitlementManagementEndpointsTests
{
    [Fact]
    public async Task GrantThenList_WithPermissions_RoundTrips()
    {
        await using var app = await CreateAppAsync();

        var grantClient = app.GetTestClient();
        grantClient.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.execute");
        var grant = await grantClient.PutAsJsonAsync(
            "/api/entitlements",
            new SetEntitlementApiRequest("acme.plugin", "workspace-a", null, IsEntitled: true));
        Assert.Equal(HttpStatusCode.NoContent, grant.StatusCode);

        var listClient = app.GetTestClient();
        listClient.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.read");
        var entitlements = await listClient.GetFromJsonAsync<EntitlementApiResponse[]>("/api/entitlements");

        var granted = Assert.Single(entitlements!);
        Assert.Equal("acme.plugin", granted.PluginId);
        Assert.Equal("workspace-a", granted.WorkspaceKey);
        Assert.True(granted.IsEntitled);
    }

    [Fact]
    public async Task Revoke_RemovesFromTheList()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.execute,plugin.read");

        _ = await client.PutAsJsonAsync(
            "/api/entitlements", new SetEntitlementApiRequest("acme.plugin", "workspace-a", null, true));
        _ = await client.PutAsJsonAsync(
            "/api/entitlements", new SetEntitlementApiRequest("acme.plugin", "workspace-a", null, false));

        var entitlements = await client.GetFromJsonAsync<EntitlementApiResponse[]>("/api/entitlements");
        Assert.Empty(entitlements!);
    }

    [Fact]
    public async Task Set_BlankPluginId_ReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.execute");

        var response = await client.PutAsJsonAsync(
            "/api/entitlements", new SetEntitlementApiRequest("  ", null, null, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutPluginRead_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.execute"); // wrong scope for a read

        var response = await client.GetAsync("/api/entitlements");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Set_WithoutPluginExecute_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.read"); // read cannot grant

        var response = await client.PutAsJsonAsync(
            "/api/entitlements", new SetEntitlementApiRequest("acme.plugin", null, null, true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IPluginEntitlementStore>(
            new InMemoryPluginEntitlementStore(new BackendHostOptions()));

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEntitlementManagementEndpoints();
        await app.StartAsync();
        return app;
    }
}
