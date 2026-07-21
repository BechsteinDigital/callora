using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Domain.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Stores;

/// <summary>SIP account store backed by the plugin's own EF Core database (PLAT-260).</summary>
public sealed class EfSipAccountStore(IPluginDbContextFactory<CommunicationDbContext> dbContextFactory)
    : ISipAccountStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SipAccount>> ListAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.SipAccounts.AsNoTracking()
            .Where(x => x.WorkspaceKey == workspaceKey)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SipAccount?> GetAsync(string workspaceKey, string accountId, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.SipAccounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkspaceKey == workspaceKey && x.Id == accountId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(SipAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using var db = dbContextFactory.CreateDbContext();
        db.SipAccounts.Add(account);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(SipAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        await using var db = dbContextFactory.CreateDbContext();
        db.SipAccounts.Update(account);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string workspaceKey, string accountId, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        var row = await db.SipAccounts
            .FirstOrDefaultAsync(x => x.WorkspaceKey == workspaceKey && x.Id == accountId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return false;
        }

        db.SipAccounts.Remove(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
