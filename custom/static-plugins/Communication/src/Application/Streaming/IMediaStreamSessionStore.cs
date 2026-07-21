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

    /// <summary>Resolves a workspace-scoped session by id.</summary>
    Task<MediaStreamSession?> GetAsync(string workspaceKey, string sessionId, CancellationToken cancellationToken = default);

    /// <summary>Deletes all sessions of a workspace (used by the GDPR purge contributor). Returns the count.</summary>
    Task<int> DeleteByWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
