using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

public sealed class AdminLoginResolverTests
{
    [Fact]
    public async Task Operator_GetsPlatformScope_WorkspaceKeyIgnored()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var root = await userStore.GetByExternalIdAsync("root");

        var grant = await AdminLoginResolver.ResolveAsync(root!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Platform, grant!.Scope);
        Assert.Null(grant.WorkspaceKey);
        Assert.Empty(grant.Permissions);
    }

    [Fact]
    public async Task WorkspaceMember_WithWorkspaceKey_GetsWorkspaceScope()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(alice!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Workspace, grant!.Scope);
        Assert.Equal("workspace-a", grant.WorkspaceKey);
    }

    [Fact]
    public async Task WorkspaceAdmin_GetsLeastPrivilegePermissions()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var carol = await userStore.GetByExternalIdAsync("carol");

        var grant = await AdminLoginResolver.ResolveAsync(carol!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendRoles.Admin, grant!.Role);
        Assert.Contains(BackendPermissionKeys.FlowManage, grant.Permissions);
        Assert.DoesNotContain("*", grant.Permissions);
        Assert.DoesNotContain(BackendPermissionKeys.TenantCreate, grant.Permissions);
    }

    [Fact]
    public async Task NonOperator_WithoutWorkspaceKey_ReturnsNull()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(alice!, workspaceKey: null, userStore, rbacStore, options);

        Assert.Null(grant);
    }

    [Fact]
    public async Task WorkspaceMember_ForeignWorkspace_ReturnsNull()
    {
        var (userStore, rbacStore, options) = await SetupAsync();
        var alice = await userStore.GetByExternalIdAsync("alice");

        var grant = await AdminLoginResolver.ResolveAsync(alice!, "workspace-b", userStore, rbacStore, options);

        Assert.Null(grant);
    }

    private static async Task<(InMemoryBackendUserStore UserStore, InMemoryBackendRbacStore RbacStore, BackendHostOptions Options)> SetupAsync()
    {
        var options = new BackendHostOptions
        {
            JwtIssuer = "callora-tests",
            JwtAudience = "callora-host-api",
            JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
            RbacUserAssignments =
            [
                new BackendRbacUserAssignmentOptions { UserId = "root", Role = BackendRoles.SuperAdmin }
            ]
        };

        var userStore = new InMemoryBackendUserStore();
        await userStore.UpsertCredentialsAsync("root", "root@example.test", "Root", "pass-root");
        await userStore.UpsertCredentialsAsync("alice", "alice@example.test", "Alice", "pass-1");
        await userStore.UpsertCredentialsAsync("carol", "carol@example.test", "Carol", "pass-carol");
        userStore.AddWorkspaceMember("workspace-a", "alice");
        userStore.AddWorkspaceMember("workspace-a", "carol", BackendRoles.Admin);

        var rbacStore = new InMemoryBackendRbacStore(options);
        return (userStore, rbacStore, options);
    }
}
