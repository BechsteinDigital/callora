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

    /// <summary>Removes a surface. True when a surface was removed.</summary>
    Task<bool> DeleteAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default);
}
