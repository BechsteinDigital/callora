namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Cascading workspace deletion: removes the workspace together with all
/// workspace-bound data (GDPR, PLAT-242).
/// </summary>
public interface IWorkspaceDataPurgeService
{
    /// <summary>
    /// Deletes the workspace and its dependent data; false when the
    /// workspace does not exist.
    /// </summary>
    Task<bool> PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
