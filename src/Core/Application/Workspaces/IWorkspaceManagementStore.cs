using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

public interface IWorkspaceManagementStore
{
    Task<IReadOnlyList<WorkspaceSnapshot>> ListAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSnapshot?> GetAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<WorkspaceThemeAssignmentSnapshot?> GetThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<WorkspaceUpsertResult> UpsertAsync(
        string tenantKey,
        string workspaceKey,
        string displayName,
        string workspaceType,
        bool isActive,
        string? publicBaseUrl = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSnapshot?> ResolveByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<WorkspaceThemeAssignmentSnapshot?> UpsertThemeAssignmentAsync(
        string workspaceKey,
        string themePluginId,
        string themeVersion,
        string? assignedBy,
        CancellationToken cancellationToken = default);

    Task<bool> ClearThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the workspace's surface access policy. Returns the updated snapshot, or null
    /// when the workspace does not exist.
    /// </summary>
    Task<WorkspaceSnapshot?> SetSurfaceAccessPolicyAsync(
        string workspaceKey,
        SurfaceAccessPolicy policy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceMemberSnapshot>> ListMembersAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<WorkspaceMemberUpsertResult> UpsertMemberAsync(
        string workspaceKey,
        string userExternalId,
        string role,
        CancellationToken cancellationToken = default);

    Task<WorkspaceMemberDeleteResult> RemoveMemberAsync(
        string workspaceKey,
        string userExternalId,
        CancellationToken cancellationToken = default);
}
