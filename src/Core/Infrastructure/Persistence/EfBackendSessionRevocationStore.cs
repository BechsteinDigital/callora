using Callora.Core.Application.Security;
using Callora.Core.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Database-backed session revocation (#105). Durable on purpose: an in-memory list
/// would resurrect every logged-out token on restart.
/// </summary>
public sealed class EfBackendSessionRevocationStore(HostPersistenceDbContext dbContext)
    : IBackendSessionRevocationStore
{
    public async Task RevokeAsync(
        string tokenId,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var normalized = tokenId.Trim();
        var existing = await dbContext.BackendRevokedSessions
            .SingleOrDefaultAsync(x => x.TokenId == normalized, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return;
        }

        dbContext.BackendRevokedSessions.Add(new BackendRevokedSession
        {
            TokenId = normalized,
            ExpiresAtUtc = expiresAtUtc,
            RevokedAtUtc = DateTimeOffset.UtcNow
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Two logouts of the same session raced; the row exists either way.
            dbContext.ChangeTracker.Clear();
        }
    }

    public Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return Task.FromResult(false);
        }

        var normalized = tokenId.Trim();
        var nowUtc = DateTimeOffset.UtcNow;
        return dbContext.BackendRevokedSessions
            .AsNoTracking()
            .AnyAsync(x => x.TokenId == normalized && x.ExpiresAtUtc > nowUtc, cancellationToken);
    }

    public Task<int> PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        return dbContext.BackendRevokedSessions
            .Where(x => x.ExpiresAtUtc <= nowUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
