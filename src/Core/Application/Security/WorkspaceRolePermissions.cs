using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Maps a <see cref="Callora.Core.Domain.Workspaces.WorkspaceMembership"/>
/// role to the permission set it grants inside its workspace.
/// <para>
/// DECISION: workspace roles never receive platform permissions
/// (tenant.*, plugin.*, role.*, workspace.*, extension.*, config.update) —
/// those stay with <see cref="BackendRoles.SuperAdmin"/>. A workspace role
/// must never get "*", or it would satisfy <c>RequirePermission</c> on
/// platform endpoints too.
/// </para>
/// <para>
/// DECISION (#102): workspace roles never receive <c>user.*</c> <em>write</em>
/// permissions. Those operate on the global <c>BackendUser</c> — credentials,
/// erasure, data-subject export — and therefore reach every workspace the
/// victim belongs to. Workspace administration works on
/// <c>membership.*</c> instead, which the endpoints confine to the caller's own
/// workspace. <c>user.read</c> stays because the read endpoints are already
/// filtered to the caller's workspace.
/// </para>
/// </summary>
[CalloraInternal("Workspace-role permission grants — RBAC enforcement, not a plugin contract (REV2 §7.2)")]
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
        BackendPermissionKeys.MembershipRead,
        BackendPermissionKeys.MembershipUpdate,
        BackendPermissionKeys.MembershipDelete
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
