using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Workspaces;

internal sealed class EmptyWorkspaceManagementStore : IWorkspaceManagementStore
{
    public Task<IReadOnlyList<WorkspaceSnapshot>> ListAsync(
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkspaceSnapshot>>([]);

    public Task<WorkspaceSnapshot?> GetAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkspaceSnapshot?>(string.IsNullOrWhiteSpace(workspaceKey)
            ? null
            : new WorkspaceSnapshot(
                TenantKey: "__default_tenant__",
                WorkspaceKey: workspaceKey.Trim(),
                DisplayName: workspaceKey.Trim(),
                WorkspaceType: "default",
                IsActive: true,
                TenantIsActive: true,
                PublicHost: null,
                ThemePluginId: null,
                ThemeVersion: null,
                ThemeAssignedBy: null,
                ThemeAssignedAtUtc: null,
                CreatedAtUtc: DateTimeOffset.UnixEpoch,
                UpdatedAtUtc: DateTimeOffset.UnixEpoch));

    public Task<WorkspaceThemeAssignmentSnapshot?> GetThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(null);
        }

        return Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(
            new WorkspaceThemeAssignmentSnapshot(
                workspaceKey.Trim(),
                null,
                null,
                null,
                null));
    }

    public Task<WorkspaceUpsertResult> UpsertAsync(
        string tenantKey,
        string workspaceKey,
        string displayName,
        string workspaceType,
        bool isActive,
        string? defaultSurfaceBaseUrl = null,
        string? publicHost = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkspaceUpsertResult(WorkspaceUpsertStatus.TenantNotFound));

    public Task<WorkspaceSnapshot?> ResolveByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkspaceSnapshot?>(null);

    public Task<WorkspaceSurfaceSnapshot?> ResolveSurfaceByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkspaceSurfaceSnapshot?>(null);

    public Task<bool> RemoveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<WorkspaceThemeAssignmentSnapshot?> UpsertThemeAssignmentAsync(
        string workspaceKey,
        string themePluginId,
        string themeVersion,
        string? assignedBy,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<WorkspaceThemeAssignmentSnapshot?>(null);

    public Task<bool> ClearThemeAssignmentAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<IReadOnlyList<WorkspaceMemberSnapshot>> ListMembersAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WorkspaceMemberSnapshot>>([]);

    public Task<WorkspaceMemberUpsertResult> UpsertMemberAsync(
        string workspaceKey,
        string userExternalId,
        string role,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkspaceMemberUpsertResult(WorkspaceMemberUpsertStatus.WorkspaceNotFound));

    public Task<WorkspaceMemberDeleteResult> RemoveMemberAsync(
        string workspaceKey,
        string userExternalId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new WorkspaceMemberDeleteResult(WorkspaceMemberDeleteStatus.WorkspaceNotFound));
}
