namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Persistence for workspace surfaces (ADR-014 §5): the N access surfaces of a workspace.
/// </summary>
public interface IWorkspaceSurfaceStore
{
    /// <summary>All surfaces of the workspace, ordered by surface key.</summary>
    Task<IReadOnlyList<WorkspaceSurfaceSnapshot>> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>One surface by workspace + surface key, or null.</summary>
    Task<WorkspaceSurfaceSnapshot?> GetAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a surface. Returns the stored snapshot, or null when the
    /// workspace does not exist.
    /// </summary>
    Task<WorkspaceSurfaceSnapshot?> UpsertAsync(
        string workspaceKey,
        WorkspaceSurfaceInput input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns or clears the surface's identity provider and stamps the audit fields
    /// in one write (ADR-017 §5.2). Separate from <see cref="UpsertAsync"/> on purpose:
    /// a surface edit carries no identity fields, so it can never clear the binding as
    /// a side effect, and every assignment records who did it and when.
    /// </summary>
    /// <param name="workspaceKey">Workspace owning the surface.</param>
    /// <param name="surfaceKey">Surface to assign.</param>
    /// <param name="pluginId">Plugin to assign, or null to clear the binding.</param>
    /// <param name="version">Version of that plugin, or null when clearing.</param>
    /// <param name="assignedBy">Operator performing the assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored snapshot, or null when workspace or surface does not exist.</returns>
    Task<WorkspaceSurfaceSnapshot?> AssignIdentityProviderAsync(
        string workspaceKey,
        string surfaceKey,
        string? pluginId,
        string? version,
        string? assignedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a surface. True when a surface was removed.</summary>
    Task<bool> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default);
}
