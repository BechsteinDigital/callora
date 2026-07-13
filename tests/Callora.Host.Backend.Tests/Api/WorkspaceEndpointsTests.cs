using System.Net;
using System.Net.Http.Json;
using Callora.Host.Backend.Api;
using Callora.Host.Workspace.Api;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Api;

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
        var members = await listClient.GetFromJsonAsync<WorkspaceMemberApiResponse[]>(
            "/api/workspaces/workspace-a/members");
        Assert.NotNull(members);
        Assert.Contains(members!, x => x.UserId == "alice" && x.Role == "owner");

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
        Assert.Equal("tenant-a", payload!.TenantKey);
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
                PublicBaseUrl: "localhost/dialer"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceApiResponse>();
        Assert.NotNull(payload);
        Assert.Equal("localhost/dialer", payload!.PublicBaseUrl);
        Assert.Equal("localhost", payload.PublicHost);
        Assert.Equal("/dialer", payload.PublicPathPrefix);
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
                PublicBaseUrl: "://invalid"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
