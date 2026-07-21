using Callora.Core.Application.Persistence.Contracts;

namespace Callora.Plugin.Communication.Application.Compliance;

/// <summary>
/// Erases the plugin's workspace-scoped data when a workspace is purged (GDPR cascading deletion,
/// REV2 §14). The host cannot reach the plugin's dedicated schema, so the plugin exports this
/// contributor and the host purge invokes it. The actual erasure is delegated to an atomic
/// <see cref="ICommunicationWorkspaceDataPurger"/> so the operation is all-or-nothing.
/// </summary>
public sealed class CommunicationDataPurgeContributor(ICommunicationWorkspaceDataPurger purger)
    : IWorkspaceDataPurgeContributor
{
    /// <inheritdoc />
    public async Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        await purger.PurgeAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
    }
}
