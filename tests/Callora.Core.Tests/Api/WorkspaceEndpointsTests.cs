using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Workspaces;
using Callora.Core.Application.Workspaces.Events;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Callora.Core.Tests.Api;

public sealed class WorkspaceEndpointsTests
{
    [Fact]
    public async Task WorkspaceAndMembershipCrud_WithPermissions_Works()
    {
        await using var app = await CreateAppAsync();

        var upsertWorkspaceClient = app.GetTestClient();
        upsertWorkspaceClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var workspaceUpsert = await upsertWorkspaceClient.PutAsJsonAsync(
            "/api/workspaces/workspace-a",
            new UpsertWorkspaceApiRequest("tenant-a", "Workspace A", "team", true));
        Assert.Equal(HttpStatusCode.OK, workspaceUpsert.StatusCode);

        var upsertMemberClient = app.GetTestClient();
        upsertMemberClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var memberUpsert = await upsertMemberClient.PutAsJsonAsync(
            "/api/workspaces/workspace-a/members/alice",
            new UpsertWorkspaceMemberApiRequest("owner"));
        Assert.Equal(HttpStatusCode.OK, memberUpsert.StatusCode);

        var listClient = app.GetTestClient();
        listClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.read");
        var members = await listClient.GetFromJsonAsync<PagedApiResponse<WorkspaceMemberApiResponse>>(
            "/api/workspaces/workspace-a/members");
        Assert.NotNull(members);
        Assert.Contains(members!.Items, x => x.UserId == "alice" && x.Role == "owner");
        Assert.Equal(members.Items.Count, members.Total);

        var deleteMemberClient = app.GetTestClient();
        deleteMemberClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var deleteMember = await deleteMemberClient.DeleteAsync("/api/workspaces/workspace-a/members/alice");
        Assert.Equal(HttpStatusCode.NoContent, deleteMember.StatusCode);

        var deleteWorkspaceClient = app.GetTestClient();
        deleteWorkspaceClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.delete");
        var deleteWorkspace = await deleteWorkspaceClient.DeleteAsync("/api/workspaces/workspace-a");
        Assert.Equal(HttpStatusCode.NoContent, deleteWorkspace.StatusCode);
    }

    [Fact]
    public async Task SurfaceAccessPolicy_IsNoLongerAWorkspaceWideSetting()
    {
        // The access mode belongs to a surface (ADR-014 §6.1): one workspace can
        // expose a public portal and an authenticated desk at the same time, so a
        // workspace-wide policy cannot express it.
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        _ = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a",
            new UpsertWorkspaceApiRequest("tenant-a", "Workspace A", "team", true));

        var response = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-a/surface-access-policy",
            new { policy = "Authenticated" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertMember_UnknownUser_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync();

        var upsertWorkspaceClient = app.GetTestClient();
        upsertWorkspaceClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        _ = await upsertWorkspaceClient.PutAsJsonAsync(
            "/api/workspaces/workspace-a",
            new UpsertWorkspaceApiRequest("tenant-a", "Workspace A", "team", true));

        var upsertMemberClient = app.GetTestClient();
        upsertMemberClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var response = await upsertMemberClient.PutAsJsonAsync(
            "/api/workspaces/workspace-a/members/unknown-user",
            new UpsertWorkspaceMemberApiRequest("agent"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertWorkspace_ProvidedTenant_IsIgnored_AndConfiguredDefaultIsUsed()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var response = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-x",
            new UpsertWorkspaceApiRequest("tenant-missing", "Workspace X", "team", true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceApiResponse>();
        Assert.NotNull(payload);
        Assert.Equal("workspace-x", payload!.WorkspaceKey);

        // The response carries no route: an address belongs to a surface, and the
        // URL passed here configures the workspace's default surface instead.
        // Where it lands is asserted against the real store in
        // WorkspaceSurfaceResolutionIntegrationTests.
        Assert.DoesNotContain(
            "publicPathPrefix",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertWorkspace_WithoutTenant_UsesConfiguredDefaultTenant()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var response = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-default",
            new UpsertWorkspaceApiRequest(null, "Workspace Default", "team", true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceApiResponse>();
        Assert.NotNull(payload);
        Assert.Equal("tenant-a", payload!.TenantKey);
    }

    [Fact]
    public async Task WorkspaceFromOtherTenant_IsNotVisible()
    {
        await using var app = await CreateAppAsync();

        var readClient = app.GetTestClient();
        readClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.read");
        var getResponse = await readClient.GetAsync("/api/workspaces/workspace-foreign");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task UpsertWorkspace_WithPublicUrl_PersistsNormalizedRoute()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var response = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-public",
            new UpsertWorkspaceApiRequest(
                TenantKey: null,
                DisplayName: "Workspace Public",
                WorkspaceType: "team",
                IsActive: true,
                DefaultSurfaceBaseUrl: "localhost/dialer"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceApiResponse>();
        Assert.NotNull(payload);
        // No route on the workspace response: the URL configured its default
        // surface. WorkspaceSurfaceResolutionIntegrationTests asserts where it lands.
        Assert.DoesNotContain(
            "publicPathPrefix",
            await response.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertWorkspace_InvalidPublicUrl_ReturnsBadRequest()
    {
        await using var app = await CreateAppAsync();

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        var response = await client.PutAsJsonAsync(
            "/api/workspaces/workspace-invalid-public",
            new UpsertWorkspaceApiRequest(
                TenantKey: null,
                DisplayName: "Workspace Invalid",
                WorkspaceType: "team",
                IsActive: true,
                DefaultSurfaceBaseUrl: "://invalid"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpsertAndDeleteWorkspace_PublishLifecycleBusinessEvents()
    {
        await using var app = await CreateAppAsync();
        var bus = (RecordingBusinessEventBus)app.Services.GetRequiredService<IBusinessEventBus>();

        var upsertClient = app.GetTestClient();
        upsertClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        _ = await upsertClient.PutAsJsonAsync(
            "/api/workspaces/workspace-ev",
            new UpsertWorkspaceApiRequest(null, "Workspace Ev", "team", true));

        var deleteClient = app.GetTestClient();
        deleteClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.delete");
        _ = await deleteClient.DeleteAsync("/api/workspaces/workspace-ev");

        var names = bus.Published.Select(static x => x.EventName).ToArray();
        // First upsert of a new workspace → created; the purge → deleted.
        Assert.Contains(WorkspaceEventTypes.Created, names);
        Assert.Contains(WorkspaceEventTypes.Deleted, names);
    }

    [Fact]
    public async Task UpsertAndRemoveMember_PublishMembershipBusinessEvents()
    {
        await using var app = await CreateAppAsync();
        var bus = (RecordingBusinessEventBus)app.Services.GetRequiredService<IBusinessEventBus>();

        var workspaceClient = app.GetTestClient();
        workspaceClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        _ = await workspaceClient.PutAsJsonAsync(
            "/api/workspaces/workspace-m",
            new UpsertWorkspaceApiRequest(null, "Workspace M", "team", true));

        var upsertMemberClient = app.GetTestClient();
        upsertMemberClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        _ = await upsertMemberClient.PutAsJsonAsync(
            "/api/workspaces/workspace-m/members/alice",
            new UpsertWorkspaceMemberApiRequest("owner"));

        var removeMemberClient = app.GetTestClient();
        removeMemberClient.DefaultRequestHeaders.Add("X-Test-Permissions", "workspace.update");
        _ = await removeMemberClient.DeleteAsync("/api/workspaces/workspace-m/members/alice");

        var names = bus.Published.Select(static x => x.EventName).ToArray();
        Assert.Contains(WorkspaceMemberEventTypes.Assigned, names);
        Assert.Contains(WorkspaceMemberEventTypes.Removed, names);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddKnownUser("alice");
        workspaceStore.AddTenant("tenant-a");
        workspaceStore.AddTenant("tenant-b");
        _ = await workspaceStore.UpsertAsync("tenant-b", "workspace-foreign", "Workspace Foreign", "team", true);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaceStore);
        builder.Services.AddSingleton<IWorkspaceDataPurgeService>(
            new InMemoryWorkspaceDataPurgeService(workspaceStore));
        builder.Services.AddSingleton<IBusinessEventBus>(new RecordingBusinessEventBus());
        builder.Services.AddSingleton(new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a"
        });

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapWorkspaceEndpoints();
        await app.StartAsync();
        return app;
    }
}
