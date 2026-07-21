using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Lines;
using Callora.Plugin.Communication.Domain.Lines;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Stores;

/// <summary>SIP line store backed by the plugin's own EF Core database.</summary>
public sealed class EfSipLineStore(IPluginDbContextFactory<CommunicationDbContext> dbContextFactory)
    : ISipLineStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SipLine>> ListByAccountAsync(string accountId, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.SipLines.AsNoTracking()
            .Where(x => x.AccountId == accountId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SipLine?> GetAsync(string workspaceKey, string lineId, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.SipLines.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkspaceKey == workspaceKey && x.Id == lineId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(SipLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        await using var db = dbContextFactory.CreateDbContext();
        db.SipLines.Add(line);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(SipLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        await using var db = dbContextFactory.CreateDbContext();
        db.SipLines.Update(line);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string workspaceKey, string lineId, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        var row = await db.SipLines
            .FirstOrDefaultAsync(x => x.WorkspaceKey == workspaceKey && x.Id == lineId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        db.SipLines.Remove(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> CountByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.SipLines
            .CountAsync(x => x.WorkspaceKey == workspaceKey, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.SipLines
            .Where(x => x.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
