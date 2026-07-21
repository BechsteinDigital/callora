using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Application.Lines;
using Callora.Plugin.Communication.Application.Streaming;

namespace Callora.Plugin.Communication.Application.Compliance;

/// <summary>
/// Erases the plugin's workspace-scoped data when a workspace is purged (GDPR cascading
/// deletion, REV2 §14): media-stream sessions, call history, lines and accounts of that
/// workspace. The host cannot reach the plugin's dedicated schema, so the plugin exports this
/// contributor and the host purge invokes it.
/// </summary>
public sealed class CommunicationDataPurgeContributor(
    ISipAccountStore accountStore,
    ISipLineStore lineStore,
    ICallLogStore callLogStore,
    IMediaStreamSessionStore mediaStreamSessionStore) : IWorkspaceDataPurgeContributor
{
    /// <inheritdoc />
    public async Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        // Child rows first (stream sessions and call history reference calls), then lines, then accounts.
        await mediaStreamSessionStore.DeleteByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        await callLogStore.DeleteByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        await lineStore.DeleteByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        await accountStore.DeleteByWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
    }
}
