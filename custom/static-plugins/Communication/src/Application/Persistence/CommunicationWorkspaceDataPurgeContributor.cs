using Callora.Host.PluginContracts.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Application.Persistence;

/// <summary>
/// Erases the communication plugin's workspace-scoped data — call logs and SIP
/// accounts in the plugin_communication schema (PLAT-260) — when a workspace is
/// purged (REV2 §14). Exported by the plugin and invoked by the host purge.
/// </summary>
public sealed class CommunicationWorkspaceDataPurgeContributor(
    IPluginDbContextFactory<VoipDbContext> dbContextFactory) : IWorkspaceDataPurgeContributor
{
    public async Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        await db.CallLogs
            .Where(callLog => callLog.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        await db.SipAccounts
            .Where(sipAccount => sipAccount.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
