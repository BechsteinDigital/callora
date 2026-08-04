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

    /// <summary>
    /// Creates or updates a workspace and ensures it has a "default" surface.
    /// <paramref name="defaultSurfaceBaseUrl"/> is a convenience for the common
    /// one-surface case: it configures the route of that default surface. The
    /// workspace itself has no address — every route lives on a surface.
    /// </summary>
    Task<WorkspaceUpsertResult> UpsertAsync(
        string tenantKey,
        string workspaceKey,
        string displayName,
        string workspaceType,
        bool isActive,
        string? defaultSurfaceBaseUrl = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSnapshot?> ResolveByPublicRouteAsync(
        string requestHost,
        string requestPath,
        string? tenantKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the request host and path to the concrete <see cref="WorkspaceSurfaceSnapshot"/>
    /// that best matches (ADR-014 §5), rather than only its owning workspace. Callers that
    /// gate or render per surface need the surface's own <c>AccessMode</c>, <c>SurfaceKey</c>,
    /// <c>Locale</c> and theme — the workspace-level <see cref="ResolveByPublicRouteAsync"/>
    /// discards these. Returns <see langword="null"/> when no active surface (on an active
    /// workspace and tenant) matches.
    /// </summary>
    /// <param name="requestHost">The incoming request host.</param>
    /// <param name="requestPath">The incoming request path.</param>
    /// <param name="tenantKey">Optional tenant to scope resolution to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<WorkspaceSurfaceSnapshot?> ResolveSurfaceByPublicRouteAsync(
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
