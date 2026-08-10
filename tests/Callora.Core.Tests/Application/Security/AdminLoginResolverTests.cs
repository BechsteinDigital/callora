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

    [Theory]
    [InlineData(BackendRoles.SuperAdmin)]
    [InlineData("SuperAdmin")]
    [InlineData("  superadmin  ")]
    [InlineData(BackendRoles.HostApi)]
    public async Task AMembershipRoleNamingAPlatformOperator_GrantsNothing(string role)
    {
        // Die Mitgliedsrolle ist ein FREIER String: Wer membership.update hat — jeder
        // Workspace-Admin (WorkspaceRolePermissions.AdminPermissions) — schreibt sie selbst.
        // Landete sie ungeprüft im Rollen-Claim, machte EndpointAuthorizationExtensions
        // daraus über `IsInRole(SuperAdmin)` unbeschränkten Plattformzugriff: aus
        // "Admin in EINEM Workspace" würde "Operator über ALLE".
        var (userStore, rbacStore, options) = await SetupAsync();
        userStore.AddWorkspaceMember("workspace-a", "mallory", role);
        await userStore.UpsertCredentialsAsync("mallory", "m@example.test", "Mallory", "pass-m");
        var mallory = await userStore.GetByExternalIdAsync("mallory");

        var grant = await AdminLoginResolver.ResolveAsync(mallory!, "workspace-a", userStore, rbacStore, options);

        Assert.Null(grant);
    }

    [Fact]
    public async Task AConfiguredOperatorRoleName_IsAlsoRefusedAsMembershipRole()
    {
        // Nicht nur "superadmin": Die Operator-Rollen sind konfigurierbar, und jede von
        // ihnen erreicht jeden Workspace (BackendHostOptions.PlatformOperatorRoles).
        var (userStore, rbacStore, options) = await SetupAsync();
        options.PlatformOperatorRoles = [BackendRoles.SuperAdmin, "plattform-betrieb"];
        userStore.AddWorkspaceMember("workspace-a", "mallory", "plattform-betrieb");
        await userStore.UpsertCredentialsAsync("mallory", "m@example.test", "Mallory", "pass-m");
        var mallory = await userStore.GetByExternalIdAsync("mallory");

        var grant = await AdminLoginResolver.ResolveAsync(mallory!, "workspace-a", userStore, rbacStore, options);

        Assert.Null(grant);
    }

    [Fact]
    public async Task AnOrdinaryMembershipRole_StillWorks()
    {
        // Die Gegenprobe: Die Sperre darf nur Operator-Namen treffen, nicht jede Rolle.
        var (userStore, rbacStore, options) = await SetupAsync();
        userStore.AddWorkspaceMember("workspace-a", "dave", "agent");
        await userStore.UpsertCredentialsAsync("dave", "d@example.test", "Dave", "pass-d");
        var dave = await userStore.GetByExternalIdAsync("dave");

        var grant = await AdminLoginResolver.ResolveAsync(dave!, "workspace-a", userStore, rbacStore, options);

        Assert.NotNull(grant);
        Assert.Equal(BackendAuthScopes.Workspace, grant!.Scope);
        Assert.Equal("agent", grant.Role);
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
