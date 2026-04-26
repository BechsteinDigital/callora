using System.Net;
using System.Net.Http.Json;
using Callora.Host.Backend.Api;
using Callora.Host.Workspace.Api;
using Callora.Host.Backend.Application.Abstractions.Tenants;
using Callora.Host.Backend.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Api;

public sealed class TenantEndpointsTests
{
    [Fact]
    public async Task TenantLifecycleCrud_WithPermissions_Works()
    {
        await using var app = await CreateAppAsync();

        var createClient = app.GetTestClient();
        createClient.DefaultRequestHeaders.Add("X-Test-Permissions", "tenant.create");
        var createResponse = await createClient.PostAsJsonAsync(
            "/api/tenants",
            new CreateTenantApiRequest("tenant-a", "Tenant A"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listClient = app.GetTestClient();
        listClient.DefaultRequestHeaders.Add("X-Test-Permissions", "tenant.read");
        var list = await listClient.GetFromJsonAsync<TenantApiResponse[]>("/api/tenants");
        Assert.NotNull(list);
        Assert.Contains(list!, x => x.TenantKey == "tenant-a" && x.IsActive);

        var suspendClient = app.GetTestClient();
        suspendClient.DefaultRequestHeaders.Add("X-Test-Permissions", "tenant.update");
        var suspendResponse = await suspendClient.PostAsync("/api/tenants/tenant-a/suspend", null);
        Assert.Equal(HttpStatusCode.OK, suspendResponse.StatusCode);

        var activateClient = app.GetTestClient();
        activateClient.DefaultRequestHeaders.Add("X-Test-Permissions", "tenant.update");
        var activateResponse = await activateClient.PostAsync("/api/tenants/tenant-a/activate", null);
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var deleteClient = app.GetTestClient();
        deleteClient.DefaultRequestHeaders.Add("X-Test-Permissions", "tenant.delete");
        var deleteResponse = await deleteClient.DeleteAsync("/api/tenants/tenant-a");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task TenantCreate_WithoutPermission_ReturnsForbidden()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync(
            "/api/tenants",
            new CreateTenantApiRequest("tenant-a", "Tenant A"));

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
        builder.Services.AddSingleton<ITenantManagementStore>(new InMemoryTenantManagementStore());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTenantEndpoints();
        await app.StartAsync();
        return app;
    }
}
