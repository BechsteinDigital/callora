using Callora.Core.Infrastructure.Security;

namespace Callora.Core.Tests.Infrastructure.Security;

public sealed class WorkspaceRolePermissionsTests
{
    [Fact]
    public void Admin_GetsWorkspaceManagementPermissions()
    {
        var permissions = WorkspaceRolePermissions.ForRole(BackendRoles.Admin);

        Assert.Contains(BackendPermissionKeys.FlowManage, permissions);
        Assert.Contains(BackendPermissionKeys.MediaManage, permissions);
        Assert.Contains(BackendPermissionKeys.UserRead, permissions);
    }

    [Fact]
    public void Admin_NeverGetsWildcardOrPlatformPermissions()
    {
        var permissions = WorkspaceRolePermissions.ForRole(BackendRoles.Admin);

        // A workspace role must never satisfy RequirePermission on platform
        // endpoints — this is the core containment guarantee of the redesign.
        Assert.DoesNotContain("*", permissions);
        Assert.DoesNotContain(BackendPermissionKeys.TenantCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.PluginCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.RoleUpdate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.WorkspaceCreate, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.ConfigUpdate, permissions);
    }

    [Fact]
    public void Member_IsReadOnly()
    {
        var permissions = WorkspaceRolePermissions.ForRole("member");

        Assert.Contains(BackendPermissionKeys.FlowRead, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.FlowManage, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserRead, permissions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something-custom")]
    public void UnknownRole_FallsBackToReadOnlyFloor(string? role)
    {
        var permissions = WorkspaceRolePermissions.ForRole(role);

        Assert.DoesNotContain("*", permissions);
        Assert.DoesNotContain(BackendPermissionKeys.FlowManage, permissions);
        Assert.DoesNotContain(BackendPermissionKeys.UserRead, permissions);
    }
}
