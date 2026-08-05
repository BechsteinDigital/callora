using System.Collections.Concurrent;
using Callora.Core.Application.Surfaces;

namespace Callora.Core.Tests.Support;

/// <summary>In-memory <see cref="ISurfaceSessionStore"/> for surface session tests.</summary>
public sealed class InMemorySurfaceSessionStore : ISurfaceSessionStore
{
    private readonly ConcurrentDictionary<Guid, SurfaceSession> _sessions = new();

    /// <summary>Sessions currently stored.</summary>
    public IReadOnlyCollection<SurfaceSession> Sessions => _sessions.Values.ToArray();

    /// <summary>When each session was last touched.</summary>
    public ConcurrentDictionary<Guid, DateTimeOffset> LastSeen { get; } = new();

    public Task<SurfaceSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.GetValueOrDefault(sessionId));

    public Task CreateAsync(SurfaceSession session, CancellationToken cancellationToken = default)
    {
        _sessions[session.SessionId] = session;
        LastSeen[session.SessionId] = session.IssuedAtUtc;
        return Task.CompletedTask;
    }

    public Task TouchAsync(Guid sessionId, DateTimeOffset seenAtUtc, CancellationToken cancellationToken = default)
    {
        LastSeen[sessionId] = seenAtUtc;
        return Task.CompletedTask;
    }

    public Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task<int> RevokeForSurfaceAsync(
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken = default)
    {
        var doomed = _sessions.Values
            .Where(x => x.WorkspaceKey == workspaceKey && x.SurfaceKey == surfaceKey)
            .ToList();
        foreach (var session in doomed)
        {
            _sessions.TryRemove(session.SessionId, out _);
        }

        return Task.FromResult(doomed.Count);
    }

    public Task<int> PurgeExpiredAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        var doomed = _sessions.Values.Where(x => x.ExpiresAtUtc <= nowUtc).ToList();
        foreach (var session in doomed)
        {
            _sessions.TryRemove(session.SessionId, out _);
        }

        return Task.FromResult(doomed.Count);
    }
}
