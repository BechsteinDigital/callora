using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Calls;
using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Stores;

/// <summary>
/// Call-history store backed by the plugin's own EF Core database. The log change and its
/// outbox entry go through one <c>SaveChanges</c>, which is the transaction boundary that makes
/// the outbox trustworthy (#113).
/// </summary>
public sealed class EfCallLogStore(IPluginDbContextFactory<CommunicationDbContext> dbContextFactory)
    : ICallLogStore, ICallEventOutbox
{
    /// <inheritdoc />
    public async Task AddAsync(
        CallLog log,
        CallEventOutboxEntry? outboxEntry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var db = dbContextFactory.CreateDbContext();
        db.CallLogs.Add(log);
        if (outboxEntry is not null)
        {
            db.CallEventOutbox.Add(outboxEntry);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        CallLog log,
        CallEventOutboxEntry? outboxEntry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        await using var db = dbContextFactory.CreateDbContext();
        db.CallLogs.Update(log);
        if (outboxEntry is not null)
        {
            db.CallEventOutbox.Add(outboxEntry);
        }

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

    /// <inheritdoc />
    public async Task<int> PurgeEndedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.CallLogs
            .Where(x => x.EndedAt != null && x.EndedAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CallEventOutboxEntry>> ListDueAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.CallEventOutbox
            .Where(x => x.DeliveredAt == null && x.NextAttemptAt <= now)
            .OrderBy(x => x.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveAttemptAsync(CallEventOutboxEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await using var db = dbContextFactory.CreateDbContext();
        db.CallEventOutbox.Update(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PurgeDeliveredAsync(
        DateTimeOffset now,
        TimeSpan retention,
        CancellationToken cancellationToken = default)
    {
        var cutoff = now - retention;
        await using var db = dbContextFactory.CreateDbContext();
        return await db.CallEventOutbox
            .Where(x => x.DeliveredAt != null && x.DeliveredAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
