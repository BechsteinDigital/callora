using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Callora.Core.Application.Tenants;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Callora.Core.Tests.Api;

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

    /// <summary>
    /// Ein Mandant gibt die Zuweisung an seine Workspaces ab — für seinen Mandanten, nicht für den
    /// des Nachbarn.
    /// </summary>
    /// <remarks>
    /// Das Recht allein genügt hier nicht: <c>plugin.assign</c> trägt jeder Mandanten-Administrator,
    /// und der Mandantenschlüssel steht im Pfad. Ohne den Abgleich gegen die Sitzung setzte er die
    /// Delegation des Nachbarn, indem er dessen Schlüssel schreibt.
    /// </remarks>
    [Fact]
    public async Task ATenantDelegates_ForItsOwnTenantOnly()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "plugin.assign");
        client.DefaultRequestHeaders.Add("X-Test-Callora-Scope", BackendAuthScopes.Tenant);
        client.DefaultRequestHeaders.Add("X-Test-Tenant-Key", "tenant-a");

        var own = await client.PutAsJsonAsync(
            "/api/tenants/tenant-a/plugins/pbx/delegation",
            new SetTenantPluginDelegationApiRequest(true));
        var neighbour = await client.PutAsJsonAsync(
            "/api/tenants/tenant-b/plugins/pbx/delegation",
            new SetTenantPluginDelegationApiRequest(true));

        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, neighbour.StatusCode);

        var delegated = await client.GetFromJsonAsync<string[]>(
            "/api/tenants/tenant-a/plugins/delegations");
        Assert.Equal(["pbx"], delegated!);
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
        builder.Services.AddSingleton<ITenantPluginDelegationStore>(
            new InMemoryTenantPluginDelegationStore());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTenantEndpoints();
        await app.StartAsync();
        return app;
    }
}
