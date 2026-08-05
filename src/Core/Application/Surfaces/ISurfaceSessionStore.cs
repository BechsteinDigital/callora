namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Server-side storage for authenticated surface sessions (ADR-017 §8.1). Guest
/// contexts never reach it — they carry no authority and therefore need no revocation.
/// </summary>
public interface ISurfaceSessionStore
{
    /// <summary>Loads a session by id, or null when it does not exist.</summary>
    /// <param name="sessionId">Opaque session id from the cookie.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<SurfaceSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Stores a newly minted session.</summary>
    /// <param name="session">The session to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateAsync(SurfaceSession session, CancellationToken cancellationToken = default);

    /// <summary>Records that a session was used, without extending its expiry.</summary>
    /// <param name="sessionId">Session that was used.</param>
    /// <param name="seenAtUtc">When it was used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task TouchAsync(Guid sessionId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken = default);

    /// <summary>Invalidates one session immediately.</summary>
    /// <param name="sessionId">Session to revoke.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates every session of one surface — used when its identity provider
    /// changes, because a session another provider vouched for cannot stay trusted.
    /// </summary>
    /// <param name="workspaceKey">Workspace owning the surface.</param>
    /// <param name="surfaceKey">Surface whose sessions end.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of sessions invalidated.</returns>
    Task<int> RevokeForSurfaceAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>Removes sessions that expired before the given instant.</summary>
    /// <param name="nowUtc">Cut-off instant.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of sessions removed.</returns>
    Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);
}
