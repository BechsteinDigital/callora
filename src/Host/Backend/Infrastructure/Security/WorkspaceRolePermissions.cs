namespace Callora.Host.Backend.Infrastructure.Security;

/// <summary>
/// Maps a <see cref="Callora.Host.Backend.Domain.Workspaces.WorkspaceMembership"/>
/// role to the permission set it grants inside its workspace.
/// <para>
/// DECISION: workspace roles never receive platform permissions
/// (tenant.*, plugin.*, role.*, workspace.*, extension.*, config.update) —
/// those stay with <see cref="BackendRoles.SuperAdmin"/>. A workspace role
/// must never get "*", or it would satisfy <c>RequirePermission</c> on
/// platform endpoints too. The user.* grants are safe only because the user
/// endpoints are workspace-scoped (audit finding H1).
/// </para>
/// </summary>
public static class WorkspaceRolePermissions
{
    private static readonly IReadOnlyList<string> AdminPermissions =
    [
        BackendPermissionKeys.FlowRead,
        BackendPermissionKeys.FlowManage,
        BackendPermissionKeys.MediaRead,
        BackendPermissionKeys.MediaManage,
        BackendPermissionKeys.CustomFieldRead,
        BackendPermissionKeys.CustomFieldUpdate,
        BackendPermissionKeys.WebhookRead,
        BackendPermissionKeys.WebhookManage,
        BackendPermissionKeys.NotificationRead,
        BackendPermissionKeys.JobRead,
        BackendPermissionKeys.ConfigRead,
        BackendPermissionKeys.UserRead,
        BackendPermissionKeys.UserCreate,
        BackendPermissionKeys.UserUpdate,
        BackendPermissionKeys.UserDelete
    ];

    private static readonly IReadOnlyList<string> MemberPermissions =
    [
        BackendPermissionKeys.FlowRead,
        BackendPermissionKeys.MediaRead,
        BackendPermissionKeys.CustomFieldRead,
        BackendPermissionKeys.NotificationRead,
        BackendPermissionKeys.JobRead,
        BackendPermissionKeys.ConfigRead
    ];

    /// <summary>
    /// Permissions for a workspace role. The workspace administrator gets the
    /// full workspace-scoped set; every other (including unknown) role gets the
    /// read-only member floor — least privilege by default.
    /// </summary>
    public static IReadOnlyList<string> ForRole(string? workspaceRole) =>
        string.Equals(workspaceRole?.Trim(), BackendRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? AdminPermissions
            : MemberPermissions;
}
