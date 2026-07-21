using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Stores;

/// <summary>Call-history store backed by the plugin's own EF Core database.</summary>
public sealed class EfCallLogStore(IPluginDbContextFactory<CommunicationDbContext> dbContextFactory)
    : ICallLogStore
{
    /// <inheritdoc />
    public async Task AddAsync(CallLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var db = dbContextFactory.CreateDbContext();
        db.CallLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(CallLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var db = dbContextFactory.CreateDbContext();
        db.CallLogs.Update(log);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CallLog>> ListRecentAsync(string workspaceKey, int limit, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.CallLogs.AsNoTracking()
            .Where(x => x.WorkspaceKey == workspaceKey)
            .OrderByDescending(x => x.StartedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.CallLogs
            .Where(x => x.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
