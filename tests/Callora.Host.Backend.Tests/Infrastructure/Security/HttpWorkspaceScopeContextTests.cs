using System.Security.Claims;
using Callora.Host.Backend.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace Callora.Host.Backend.Tests.Infrastructure.Security;

public sealed class HttpWorkspaceScopeContextTests
{
    [Fact]
    public void WorkspaceBoundUser_IsScopedToItsWorkspace()
    {
        var context = Build(workspaceKey: "workspace-a", scope: BackendAuthScopes.Workspace);

        Assert.True(context.IsWorkspaceScoped);
        Assert.Equal("workspace-a", context.WorkspaceKey);
    }

    [Fact]
    public void PlatformOperator_IsNotScoped()
    {
        var context = Build(workspaceKey: "workspace-a", scope: BackendAuthScopes.Platform);

        Assert.False(context.IsWorkspaceScoped);
        Assert.Null(context.WorkspaceKey);
    }

    [Fact]
    public void SuperAdmin_IsNotScoped()
    {
        var context = Build(workspaceKey: "workspace-a", role: BackendRoles.SuperAdmin);

        Assert.False(context.IsWorkspaceScoped);
    }

    [Fact]
    public void WithoutHttpContext_IsNotScoped()
    {
        var context = new HttpWorkspaceScopeContext(new HttpContextAccessor());

        Assert.False(context.IsWorkspaceScoped);
        Assert.Null(context.WorkspaceKey);
    }

    private static HttpWorkspaceScopeContext Build(string? workspaceKey = null, string? scope = null, string? role = null)
    {
        var claims = new List<Claim>();
        if (workspaceKey is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.WorkspaceKey, workspaceKey));
        }
        if (scope is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.CalloraScope, scope));
        }
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
        };
        return new HttpWorkspaceScopeContext(new HttpContextAccessor { HttpContext = httpContext });
    }
}
