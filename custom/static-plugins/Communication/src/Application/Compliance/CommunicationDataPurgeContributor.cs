using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Lines;

namespace Callora.Plugin.Communication.Application.Compliance;

/// <summary>
/// Erases the plugin's workspace-scoped data when a workspace is purged (GDPR cascading
/// deletion, REV2 §14): call history, lines and accounts of that workspace. The host cannot
/// reach the plugin's dedicated schema, so the plugin exports this contributor and the host
/// purge invokes it.
/// </summary>
public sealed class CommunicationDataPurgeContributor(
    ISipAccountStore accountStore,
    ISipLineStore lineStore,
    ICallLogStore callLogStore) : IWorkspaceDataPurgeContributor
{
    /// <inheritdoc />
    public async Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        // Child rows first (call history carries the personal data), then lines, then accounts.
        await callLogStore.DeleteByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        await lineStore.DeleteByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        await accountStore.DeleteByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
    }
}
