using Callora.Plugin.Communication.Domain.Streaming;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>Workspace-scoped persistence port for <see cref="MediaStreamSession"/> (WS-stream bindings).</summary>
public interface IMediaStreamSessionStore
{
    /// <summary>Persists a new pending session.</summary>
    Task AddAsync(MediaStreamSession session, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing session (activate/close transitions).</summary>
    Task UpdateAsync(MediaStreamSession session, CancellationToken cancellationToken = default);

    /// <summary>Resolves a session by its single-use connect token (for WS-connect authorization).</summary>
    Task<MediaStreamSession?> GetByConnectTokenAsync(string connectToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes the connect token: if it belongs to a still-pending, non-expired session,
    /// activates it and returns the now-active session; otherwise returns <see langword="null"/>.
    /// Under a concurrent double-connect only one caller wins — the token stays strictly single-use.
    /// </summary>
    Task<MediaStreamSession?> TryActivateByConnectTokenAsync(
        string connectToken, DateTimeOffset now, TimeSpan timeToLive, CancellationToken cancellationToken = default);

    /// <summary>Resolves a workspace-scoped session by id.</summary>
    Task<MediaStreamSession?> GetAsync(string workspaceKey, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes sessions that closed, or whose ticket has been unusable, for longer than
    /// <paramref name="retention"/>. Returns the count. Spent and expired tickets must not
    /// accumulate (#108).
    /// </summary>
    Task<int> PurgeExpiredAsync(
        DateTimeOffset now, TimeSpan retention, CancellationToken cancellationToken = default);

    /// <summary>Deletes all sessions of a workspace (used by the GDPR purge contributor). Returns the count.</summary>
    Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
