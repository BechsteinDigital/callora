using Callora.Core.Application.Security;
using System.Security.Claims;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

public sealed class AdminContextViewTests
{
    [Fact]
    public void FromPrincipal_AggregatesIdentityRolesPermissionsAndScope()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim(ClaimTypes.Name, "Max"),
            new Claim(ClaimTypes.Email, "max@example.org"),
            new Claim(ClaimTypes.Role, "workspace-admin"),
            new Claim(BackendClaimTypes.Permission, "workspace.read"),
            new Claim(BackendClaimTypes.Permission, "workspace.settings.write"),
            new Claim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Workspace),
            new Claim(BackendClaimTypes.WorkspaceKey, "sales-de")
        ], "test"));

        var context = AdminContextView.FromPrincipal(user);

        Assert.NotNull(context);
        Assert.Equal("user-1", context.UserId);
        Assert.Equal("Max", context.DisplayName);
        Assert.Equal("max@example.org", context.Email);
        Assert.Equal(["workspace-admin"], context.Roles);
        Assert.Equal(2, context.Permissions.Count);
        Assert.Contains("workspace.read", context.Permissions);
        Assert.Equal(BackendAuthScopes.Workspace, context.Scope);
        Assert.Equal("sales-de", context.WorkspaceKey);
        Assert.False(context.IsOperator);
    }

    [Fact]
    public void FromPrincipal_PlatformScope_IsOperator()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "op-1"),
            new Claim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Platform)
        ], "test"));

        var context = AdminContextView.FromPrincipal(user);

        Assert.NotNull(context);
        Assert.True(context.IsOperator);
    }

    [Fact]
    public void FromPrincipal_SuperAdminRole_IsOperator()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "sa-1"),
            new Claim(ClaimTypes.Role, BackendRoles.SuperAdmin)
        ], "test"));

        var context = AdminContextView.FromPrincipal(user);

        Assert.NotNull(context);
        Assert.True(context.IsOperator);
    }

    [Fact]
    public void FromPrincipal_NoSubject_ReturnsNull()
    {
        Assert.Null(AdminContextView.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
