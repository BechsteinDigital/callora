using System.Security.Claims;
using Callora.Host.Backend.Infrastructure.Security;
using Xunit;

namespace Callora.Host.Backend.Tests.Infrastructure.Security;

public sealed class WorkspaceScopeEvaluatorTests
{
    [Fact]
    public void PrincipalWithoutWorkspaceBinding_HasPlatformWideAccess()
    {
        var user = BuildUser();

        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-a"));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, null));
    }

    [Fact]
    public void BoundPrincipal_OnlyAccessesItsOwnWorkspace()
    {
        var user = BuildUser(workspaceKey: "workspace-a");

        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-a"));
        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "WORKSPACE-A"));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-b"));
    }

    [Fact]
    public void BoundPrincipal_WithoutRequestedWorkspace_IsRejected()
    {
        var user = BuildUser(workspaceKey: "workspace-a");

        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, null));
        Assert.False(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, " "));
    }

    [Fact]
    public void AdminRole_OverridesWorkspaceBinding()
    {
        var user = BuildUser(workspaceKey: "workspace-a", role: BackendRoles.Admin);

        Assert.True(WorkspaceScopeEvaluator.HasWorkspaceAccess(user, "workspace-b"));
    }

    private static ClaimsPrincipal BuildUser(string? workspaceKey = null, string? role = null)
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

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }
}
