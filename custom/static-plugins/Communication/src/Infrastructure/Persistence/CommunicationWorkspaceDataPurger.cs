using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Compliance;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Infrastructure.Persistence;

/// <summary>
/// Erases every table of a workspace in one transaction: media-stream sessions, call history,
/// lines, then accounts (child → parent, matching the line→account workspace-FK). A single
/// DbContext and an explicit transaction make the purge atomic — a failure on any table rolls the
/// whole operation back, so a workspace is never left partially purged.
/// </summary>
public sealed class CommunicationWorkspaceDataPurger(IPluginDbContextFactory<CommunicationDbContext> dbContextFactory)
    : ICommunicationWorkspaceDataPurger
{
    /// <inheritdoc />
    public async Task PurgeAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        await using var db = dbContextFactory.CreateDbContext();
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        // Child → parent so no workspace-scoped foreign key is violated mid-purge.
        await db.MediaStreamSessions.Where(x => x.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.CallEventOutbox.Where(x => x.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.CallLogs.Where(x => x.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.SipAccounts.Where(x => x.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
