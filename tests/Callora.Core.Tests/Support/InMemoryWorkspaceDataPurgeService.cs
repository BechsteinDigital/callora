using Callora.Core.Application.Workspaces;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Test double for the cascading workspace purge: delegates to the
/// in-memory workspace store's plain removal.
/// </summary>
public sealed class InMemoryWorkspaceDataPurgeService(IWorkspaceManagementStore workspaceStore)
    : IWorkspaceDataPurgeService
{
    public Task<bool> PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
        workspaceStore.RemoveAsync(workspaceKey, cancellationToken);
}
