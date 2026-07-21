using Callora.Core.Application.Persistence.Contracts;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Domain.Streaming;
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Infrastructure.Persistence.Stores;

/// <summary>Media-stream-session store backed by the plugin's own EF Core database.</summary>
public sealed class EfMediaStreamSessionStore(IPluginDbContextFactory<CommunicationDbContext> dbContextFactory)
    : IMediaStreamSessionStore
{
    /// <inheritdoc />
    public async Task AddAsync(MediaStreamSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var db = dbContextFactory.CreateDbContext();
        db.MediaStreamSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(MediaStreamSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await using var db = dbContextFactory.CreateDbContext();
        db.MediaStreamSessions.Update(session);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MediaStreamSession?> GetByConnectTokenAsync(string connectToken, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.MediaStreamSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ConnectToken == connectToken, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MediaStreamSession?> TryActivateByConnectTokenAsync(
        string connectToken, DateTimeOffset now, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();

        // Atomic compare-and-swap: one UPDATE flips Pending → Active only while the token is still
        // pending and within its TTL. The predicate mirrors MediaStreamSession.CanActivate; encoding
        // it in the WHERE is what makes activation atomic, so a concurrent double-connect cannot both
        // win — the loser's UPDATE matches zero rows.
        var earliestValidCreation = now - timeToLive;
        var activated = await db.MediaStreamSessions
            .Where(x => x.ConnectToken == connectToken
                && x.Status == MediaStreamSessionStatus.Pending
                && x.CreatedAt >= earliestValidCreation)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, MediaStreamSessionStatus.Active)
                    .SetProperty(x => x.StartedAt, now),
                cancellationToken)
            .ConfigureAwait(false);

        if (activated == 0)
        {
            return null; // Unknown, expired or already consumed.
        }

        return await db.MediaStreamSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ConnectToken == connectToken, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MediaStreamSession?> GetAsync(string workspaceKey, string sessionId, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.MediaStreamSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WorkspaceKey == workspaceKey && x.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        await using var db = dbContextFactory.CreateDbContext();
        return await db.MediaStreamSessions
            .Where(x => x.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
