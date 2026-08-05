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
        // Only the hash is stored (#108), so the presented token is hashed to look it up.
        var tokenHash = MediaStreamSession.HashToken(connectToken);
        await using var db = dbContextFactory.CreateDbContext();
        return await db.MediaStreamSessions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ConnectTokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MediaStreamSession?> TryActivateByConnectTokenAsync(
        string connectToken, DateTimeOffset now, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        var tokenHash = MediaStreamSession.HashToken(connectToken);
        await using var db = dbContextFactory.CreateDbContext();

        // Atomic compare-and-swap: one UPDATE flips Pending → Active only while the token is still
        // pending and inside its validity window. The predicate mirrors
        // MediaStreamSession.CanActivate — including the upper bound, without which a
        // future-dated row would stay redeemable forever (#108). Encoding it in the WHERE is what
        // makes activation atomic, so a concurrent double-connect cannot both win — the loser's
        // UPDATE matches zero rows.
        var earliestValidCreation = now - timeToLive;
        var activated = await db.MediaStreamSessions
            .Where(x => x.ConnectTokenHash == tokenHash
                && x.Status == MediaStreamSessionStatus.Pending
                && x.CreatedAt >= earliestValidCreation
                && x.CreatedAt <= now)
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
            .FirstOrDefaultAsync(x => x.ConnectTokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<int> PurgeExpiredAsync(
        DateTimeOffset now, TimeSpan retention, CancellationToken cancellationToken = default)
    {
        // Spent and expired tickets must not accumulate (#108): a closed session, or one
        // whose token has been unusable for longer than the retention window, is dropped.
        var cutoff = now - retention;
        await using var db = dbContextFactory.CreateDbContext();
        return await db.MediaStreamSessions
            .Where(x => (x.EndedAt != null && x.EndedAt <= cutoff)
                || (x.EndedAt == null && x.CreatedAt <= cutoff))
            .ExecuteDeleteAsync(cancellationToken)
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
    public async Task<int> CloseByCallAsync(
        string workspaceKey, string callId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        // One set-based UPDATE rather than load-modify-save: the call is already gone, so there is
        // nothing to reconcile per row, and a hang-up must not wait on a page of sessions. Already
        // closed rows are excluded so EndedAt keeps the time the stream actually stopped.
        await using var db = dbContextFactory.CreateDbContext();
        return await db.MediaStreamSessions
            .Where(x => x.WorkspaceKey == workspaceKey
                && x.CallId == callId
                && x.Status != MediaStreamSessionStatus.Closed)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, MediaStreamSessionStatus.Closed)
                    .SetProperty(x => x.EndedAt, now),
                cancellationToken)
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
