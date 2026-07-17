using Callora.Core.Application.Security;
using System.Security.Claims;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

public sealed class WorkspaceScopeEvaluatorTests
{
    [Fact]
    public void PlatformScopedPrincipal_HasPlatformWideAccess()
    {
        var user = BuildUser(calloraScope: BackendAuthScopes.Platform);

        Assert.True(WorkspaceScopeEvaluator.IsOperator(user));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-a"));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, null));
    }

    [Fact]
    public void PrincipalWithoutScopeOrBinding_IsRejected()
    {
        var user = BuildUser();

        Assert.False(WorkspaceScopeEvaluator.IsOperator(user));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-a"));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, null));
    }

    [Fact]
    public void WorkspaceScopedPrincipal_IsNeverOperator()
    {
        var user = BuildUser(workspaceKey: "workspace-a", calloraScope: BackendAuthScopes.Workspace);

        Assert.False(WorkspaceScopeEvaluator.IsOperator(user));
    }

    [Fact]
    public void BoundPrincipal_OnlyAccessesItsOwnWorkspace()
    {
        var user = BuildUser(workspaceKey: "workspace-a", calloraScope: BackendAuthScopes.Workspace);

        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-a"));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "WORKSPACE-A"));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-b"));
    }

    [Fact]
    public void LegacyBoundPrincipal_WithoutScopeClaim_StaysLockedToItsWorkspace()
    {
        var user = BuildUser(workspaceKey: "workspace-a");

        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-a"));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-b"));
        Assert.False(WorkspaceScopeEvaluator.IsOperator(user));
    }

    [Fact]
    public void BoundPrincipal_WithoutRequestedWorkspace_IsRejected()
    {
        var user = BuildUser(workspaceKey: "workspace-a", calloraScope: BackendAuthScopes.Workspace);

        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, null));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, " "));
    }

    [Fact]
    public void SuperAdminRole_OverridesWorkspaceBinding()
    {
        var user = BuildUser(workspaceKey: "workspace-a", role: BackendRoles.SuperAdmin);

        Assert.True(WorkspaceScopeEvaluator.IsOperator(user));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-b"));
    }

    [Fact]
    public void AdminRole_DoesNotOverrideWorkspaceBinding()
    {
        // Admin is a workspace role now, not a platform operator (RBAC redesign):
        // it must stay locked to its own workspace.
        var user = BuildUser(workspaceKey: "workspace-a", role: BackendRoles.Admin);

        Assert.False(WorkspaceScopeEvaluator.IsOperator(user));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-a"));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-b"));
    }

    private static ClaimsPrincipal BuildUser(
        string? workspaceKey = null,
        string? role = null,
        string? calloraScope = null)
    {
        var claims = new List<Claim>();
        if (workspaceKey is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.WorkspaceKey, workspaceKey));
        }
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }
        if (calloraScope is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.CalloraScope, calloraScope));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }
}
