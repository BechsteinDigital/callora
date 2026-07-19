using Callora.Administration.Api;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Workspaces;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Callora.Core.Tests.Api;

public sealed class SurfaceEndpointsTests
{
    private static UpsertSurfaceApiRequest Surface(string accessMode = "Authenticated") => new(
        DisplayName: "Customer Portal",
        SurfaceType: "spa",
        PublicBaseUrl: null,
        PublicHost: "portal.example.de",
        PublicPathPrefix: "/",
        AccessMode: accessMode,
        Locale: "de",
        TemplatePluginId: null,
        TemplateVersion: null,
        ThemePluginId: "customer.theme",
        ThemeVersion: "1.0.0",
        IsActive: true);

    [Fact]
    public async Task SurfaceCrud_WithPermissions_Works()
    {
        await using var app = await CreateAppAsync();

        var upsertClient = app.GetTestClient();
        upsertClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var upsert = await upsertClient.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surfaces/portal", Surface());
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        var created = await upsert.Content.ReadFromJsonAsync<SurfaceApiResponse>();
        Assert.Equal("portal", created!.SurfaceKey);
        Assert.Equal("Authenticated", created.AccessMode);

        var listClient = app.GetTestClient();
        listClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.read");
        var surfaces = await listClient.GetFromJsonAsync<SurfaceApiResponse[]>(
            "/api/workspaces/workspace-a/surfaces");
        Assert.Contains(surfaces!, s => s.SurfaceKey == "portal");

        var deleteClient = app.GetTestClient();
        deleteClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var delete = await deleteClient.DeleteAsync("/api/workspaces/workspace-a/surfaces/portal");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task Upsert_InvalidAccessMode_ReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");

        var response = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surfaces/x", Surface(accessMode: "Nonsense"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Surfaces_ForeignTenantWorkspace_ReturnNotFound()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.read");

        var response = await client.GetAsync("/api/workspaces/workspace-foreign/surfaces");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        workspaceStore.AddTenant("tenant-b");
        _ = await workspaceStore.UpsertAsync("tenant-a", "workspace-a", "Workspace A", "team", true);
        _ = await workspaceStore.UpsertAsync("tenant-b", "workspace-foreign", "Foreign", "team", true);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaceStore);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(new InMemoryWorkspaceSurfaceStore());
        builder.Services.AddSingleton(new BackendHostOptions { DefaultTenantKey = "tenant-a" });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSurfaceEndpoints();
        await app.StartAsync();
        return app;
    }
}
