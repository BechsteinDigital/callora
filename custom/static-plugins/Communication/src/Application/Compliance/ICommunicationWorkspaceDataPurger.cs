namespace Callora.Plugin.Communication.Application.Compliance;

/// <summary>
/// Atomically erases all of a workspace's communication data in a single transaction. The purge
/// contributor delegates here so a workspace erasure either fully completes or fully rolls back —
/// a compliance operation must never leave a partially-purged workspace behind.
/// </summary>
public interface ICommunicationWorkspaceDataPurger
{
    /// <summary>Erases every table of the workspace in one transaction.</summary>
    Task PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
