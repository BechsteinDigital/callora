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
    private static UpsertSurfaceApiRequest Surface(string authentication = "SurfaceIdentity") => new(
        DisplayName: "Customer Portal",
        SurfaceType: "spa",
        PublicBaseUrl: null,
        PublicHost: "portal.example.de",
        PublicPathPrefix: "/",
        Authentication: authentication,
        Locale: "de",
        TemplatePluginId: null,
        TemplateVersion: null,
        ThemePluginId: "customer.theme",
        ThemeVersion: "1.0.0",
        IsActive: true);

    [Fact]
    public async Task DeletingANodeWithChildrenIsRefusedWithAConflict()
    {
        // Ohne diese Prüfung liefe der Versuch in den Restrict-Fremdschlüssel und käme als
        // Serverfehler beim Operator an — und ein 500 sagt nicht, dass da eine Unterseite hängt.
        // 409 und nicht 404: Der Knoten ist da, er lässt sich nur nicht so löschen.
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        await client.PutAsJsonAsync("/api/workspaces/workspace-a/surfaces/portal", Surface());
        await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surfaces/partner",
            Surface() with { ParentSurfaceKey = "portal", PublicPathPrefix = "partner" });

        var refused = await client.DeleteAsync("/api/workspaces/workspace-a/surfaces/portal");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        // Erst das Kind, dann der Elternteil — und dann geht es.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync("/api/workspaces/workspace-a/surfaces/partner")).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.DeleteAsync("/api/workspaces/workspace-a/surfaces/portal")).StatusCode);
    }

    [Fact]
    public async Task AChildIsCreatedUnderItsParentAndCarriesItBack()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        await client.PutAsJsonAsync("/api/workspaces/workspace-a/surfaces/portal", Surface());

        var created = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surfaces/partner",
            Surface() with { ParentSurfaceKey = "portal", PublicPathPrefix = "partner", Position = 2 });

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<SurfaceApiResponse>();
        Assert.Equal("portal", body!.ParentSurfaceKey);
        Assert.Equal(2, body.Position);
    }

    [Fact]
    public async Task AParentThatWouldCreateACycleIsRefused()
    {
        // Der Zyklus wird beim Schreiben abgelehnt, nicht beim Auflösen: Ein Zyklus, der erst
        // beim Rendern aufliefe, wäre eine Endlosschleife für jeden Besucher.
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        await client.PutAsJsonAsync("/api/workspaces/workspace-a/surfaces/portal", Surface());
        await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surfaces/partner",
            Surface() with { ParentSurfaceKey = "portal", PublicPathPrefix = "partner" });

        var cycle = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surfaces/portal",
            Surface() with { ParentSurfaceKey = "partner" });

        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
    }

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
        Assert.Equal("SurfaceIdentity", created.Authentication);

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
    public async Task Upsert_InvalidAuthentication_ReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");

        var response = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surfaces/x", Surface(authentication: "Nonsense"));

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
