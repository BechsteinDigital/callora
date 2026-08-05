using Callora.Administration.Api;
using Callora.Core.Application.Audit;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Workspaces;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Callora.Core.Tests.Api;

/// <summary>
/// The boundary between workspace-membership administration and global identity
/// administration (#102). A workspace administrator manages who belongs to its
/// own workspace; it never reaches the global <c>BackendUser</c> of a member who
/// also belongs elsewhere.
/// </summary>
public sealed class WorkspaceAdminIdentityBoundaryTests
{
    private const string HomeWorkspace = "workspace-a";
    private const string ForeignWorkspace = "workspace-b";

    /// <summary>Victim: member of both workspaces.</summary>
    private const string SharedUser = "mallory-victim";

    /// <summary>Platform operator who also holds a membership in the home workspace.</summary>
    private const string OperatorMember = "root";

    [Fact]
    public void WorkspaceAdminRole_HasNoGlobalIdentityWritePermission()
    {
        var permissions = WorkspaceRolePermissions.ForRole(BackendRoles.Admin);

        Assert.DoesNotContain(BackendPermissionKeys.UserCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserDelete, permissions);
        Assert.Contains(BackendPermissionKeys.MembershipRead, permissions);
        Assert.Contains(BackendPermissionKeys.MembershipUpdate, permissions);
        Assert.Contains(BackendPermissionKeys.MembershipDelete, permissions);
    }

    [Fact]
    public async Task WorkspaceAdmin_CannotReplaceGlobalCredentialsOfSharedMember()
    {
        await using var app = await CreateAppAsync();
        var client = WorkspaceAdminClient(app);

        var response = await client.PutAsJsonAsync(
            $"/api/users/{SharedUser}",
            new UpdateBackendUserApiRequest("attacker@example.test", "Owned", "attacker-password"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var store = app.Services.GetRequiredService<IBackendUserStore>();
        var victim = await store.GetByExternalIdAsync(SharedUser);
        Assert.Equal("victim@example.test", victim!.Email);
    }

    [Fact]
    public async Task WorkspaceAdmin_CannotEraseSharedMember()
    {
        await using var app = await CreateAppAsync();
        var client = WorkspaceAdminClient(app);

        var response = await client.DeleteAsync($"/api/users/{SharedUser}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // The account and — critically — the foreign membership survive.
        var store = app.Services.GetRequiredService<IBackendUserStore>();
        Assert.NotNull(await store.GetByExternalIdAsync(SharedUser));
        Assert.True(await store.IsWorkspaceMemberAsync(SharedUser, ForeignWorkspace));
    }

    [Fact]
    public async Task WorkspaceAdmin_CannotExportSharedMember_SoForeignWorkspacesStayHidden()
    {
        await using var app = await CreateAppAsync();
        var client = WorkspaceAdminClient(app);

        var response = await client.GetAsync($"/api/users/{SharedUser}/data-export");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ForeignWorkspace, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceAdmin_CannotTouchOperatorWhoIsAlsoAWorkspaceMember()
    {
        await using var app = await CreateAppAsync();
        var client = WorkspaceAdminClient(app);

        var update = await client.PutAsJsonAsync(
            $"/api/users/{OperatorMember}",
            new UpdateBackendUserApiRequest("takeover@example.test", "Takeover", "takeover-password"));
        var delete = await client.DeleteAsync($"/api/users/{OperatorMember}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);

        var store = app.Services.GetRequiredService<IBackendUserStore>();
        var operatorAccount = await store.GetByExternalIdAsync(OperatorMember);
        Assert.Equal("root@example.test", operatorAccount!.Email);
    }

    [Fact]
    public async Task WorkspaceAdmin_ManagesMembershipOfItsOwnWorkspace()
    {
        await using var app = await CreateAppAsync();
        var client = WorkspaceAdminClient(app);

        var list = await client.GetAsync($"/api/workspaces/{HomeWorkspace}/members");
        var upsert = await client.PutAsJsonAsync(
            $"/api/workspaces/{HomeWorkspace}/members/{SharedUser}",
            new UpsertWorkspaceMemberApiRequest(BackendRoles.Admin));
        var remove = await client.DeleteAsync($"/api/workspaces/{HomeWorkspace}/members/{SharedUser}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.OK, upsert.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
    }

    [Fact]
    public async Task WorkspaceAdmin_CannotManageMembershipOfAForeignWorkspace()
    {
        await using var app = await CreateAppAsync();
        var client = WorkspaceAdminClient(app);

        var list = await client.GetAsync($"/api/workspaces/{ForeignWorkspace}/members");
        var upsert = await client.PutAsJsonAsync(
            $"/api/workspaces/{ForeignWorkspace}/members/{SharedUser}",
            new UpsertWorkspaceMemberApiRequest(BackendRoles.Admin));
        var remove = await client.DeleteAsync($"/api/workspaces/{ForeignWorkspace}/members/{SharedUser}");

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, upsert.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, remove.StatusCode);
    }

    [Fact]
    public async Task Operator_KeepsGlobalIdentityAdministration()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Permissions",
            $"{BackendPermissionKeys.UserRead},{BackendPermissionKeys.UserUpdate}");
        client.DefaultRequestHeaders.Add("X-Test-Callora-Scope", BackendAuthScopes.Platform);

        var update = await client.PutAsJsonAsync(
            $"/api/users/{SharedUser}",
            new UpdateBackendUserApiRequest("renamed@example.test", "Renamed", null));
        var export = await client.GetAsync($"/api/users/{SharedUser}/data-export");

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
    }

    /// <summary>
    /// A workspace administrator of <see cref="HomeWorkspace"/> — the exact
    /// principal the login endpoint issues for that role.
    /// </summary>
    private static HttpClient WorkspaceAdminClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(
            "X-Test-Permissions",
            string.Join(',', WorkspaceRolePermissions.ForRole(BackendRoles.Admin)));
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", HomeWorkspace);
        client.DefaultRequestHeaders.Add("X-Test-Callora-Scope", BackendAuthScopes.Workspace);
        return client;
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var userStore = new InMemoryBackendUserStore();
        await userStore.UpsertCredentialsAsync(SharedUser, "victim@example.test", "Victim", "victim-password");
        await userStore.UpsertCredentialsAsync(OperatorMember, "root@example.test", "Root", "root-password");
        userStore.AddWorkspaceMember(HomeWorkspace, SharedUser);
        userStore.AddWorkspaceMember(ForeignWorkspace, SharedUser);
        userStore.AddWorkspaceMember(HomeWorkspace, OperatorMember);

        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        workspaceStore.AddKnownUser(SharedUser);
        workspaceStore.AddKnownUser(OperatorMember);
        _ = await workspaceStore.UpsertAsync("tenant-a", HomeWorkspace, "Workspace A", "team", true);
        _ = await workspaceStore.UpsertAsync("tenant-a", ForeignWorkspace, "Workspace B", "team", true);
        _ = await workspaceStore.UpsertMemberAsync(HomeWorkspace, SharedUser, BackendRoles.Admin);
        _ = await workspaceStore.UpsertMemberAsync(ForeignWorkspace, SharedUser, BackendRoles.Admin);

        var options = new BackendHostOptions { DefaultTenantKey = "tenant-a" };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IBackendUserStore>(userStore);
        builder.Services.AddSingleton<IUserDataSubjectService>(new MembershipRevealingDataSubjectService(userStore));
        builder.Services.AddSingleton<IBackendRbacStore>(new InMemoryBackendRbacStore(options));
        builder.Services.AddSingleton<IWorkspaceManagementStore>(workspaceStore);
        builder.Services.AddSingleton<IWorkspaceDataPurgeService>(
            new InMemoryWorkspaceDataPurgeService(workspaceStore));
        builder.Services.AddSingleton<IBusinessEventBus>(new RecordingBusinessEventBus());
        builder.Services.AddSingleton<IHostAuditStore, InMemoryHostAuditStore>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapUserEndpoints();
        app.MapWorkspaceEndpoints();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Mirrors the production export: it discloses every workspace the subject
    /// belongs to. The test asserts that this payload never reaches a
    /// workspace-scoped caller.
    /// </summary>
    private sealed class MembershipRevealingDataSubjectService(InMemoryBackendUserStore userStore)
        : IUserDataSubjectService
    {
        public async Task<UserDataExport?> ExportAsync(
            string externalId,
            CancellationToken cancellationToken = default)
        {
            var user = await userStore.GetByExternalIdAsync(externalId, cancellationToken);
            if (user is null)
            {
                return null;
            }

            var memberships = new List<UserDataExportMembership>();
            foreach (var workspaceKey in new[] { HomeWorkspace, ForeignWorkspace })
            {
                if (await userStore.IsWorkspaceMemberAsync(externalId, workspaceKey, cancellationToken))
                {
                    var role = await userStore.GetWorkspaceRoleAsync(externalId, workspaceKey, cancellationToken);
                    memberships.Add(new UserDataExportMembership(
                        workspaceKey,
                        role ?? BackendRoles.Admin,
                        user.CreatedAtUtc));
                }
            }

            return new UserDataExport(
                user.ExternalId,
                user.Email,
                user.DisplayName,
                user.CreatedAtUtc,
                Role: null,
                memberships,
                AuditTrail: []);
        }

        public Task<bool> EraseAsync(string externalId, CancellationToken cancellationToken = default) =>
            userStore.RemoveAsync(externalId, cancellationToken);
    }
}
