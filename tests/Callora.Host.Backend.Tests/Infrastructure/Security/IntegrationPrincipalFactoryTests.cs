using Callora.Host.Backend.Domain.Integrations;
using Callora.Host.Backend.Infrastructure.Security;
using Xunit;

namespace Callora.Host.Backend.Tests.Infrastructure.Security;

public sealed class IntegrationPrincipalFactoryTests
{
    [Fact]
    public void Create_GrantsOnlyAssignedRole_NeverSuperAdminOrWildcard()
    {
        var principal = IntegrationPrincipalFactory.Create(new IntegrationCredential
        {
            Name = "billing",
            RoleName = "billing-role",
            Scope = BackendAuthScopes.Platform
        });

        Assert.Equal("integration:billing", principal.Identity!.Name);
        Assert.True(principal.IsInRole("billing-role"));
        Assert.False(principal.IsInRole(BackendRoles.SuperAdmin));
        Assert.False(principal.IsInRole(BackendRoles.HostApi));
        Assert.False(principal.HasClaim(BackendClaimTypes.Permission, "*"));
    }

    [Fact]
    public void Create_PlatformScope_IsOperatorButStillBoundedByRolePermissions()
    {
        // Platform scope crosses workspaces (operator), but access remains bounded
        // to the role's permissions — unlike the bootstrap key's wildcard grant.
        var principal = IntegrationPrincipalFactory.Create(new IntegrationCredential
        {
            Name = "billing",
            RoleName = "billing-role",
            Scope = BackendAuthScopes.Platform
        });

        Assert.True(WorkspaceScopeEvaluator.IsOperator(principal));
        Assert.False(principal.HasClaim(BackendClaimTypes.Permission, "*"));
    }

    [Fact]
    public void Create_WorkspaceScope_IsNotOperatorAndLockedToWorkspace()
    {
        var principal = IntegrationPrincipalFactory.Create(new IntegrationCredential
        {
            Name = "wsbot",
            RoleName = "reader",
            Scope = BackendAuthScopes.Workspace,
            WorkspaceKey = "workspace-a"
        });

        Assert.False(WorkspaceScopeEvaluator.IsOperator(principal));
        Assert.True(principal.HasClaim(BackendClaimTypes.WorkspaceKey, "workspace-a"));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(principal, "workspace-a"));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(principal, "workspace-b"));
    }
}
