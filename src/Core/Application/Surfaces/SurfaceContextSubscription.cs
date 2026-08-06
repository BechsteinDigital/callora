using System.Threading.Channels;
using Callora.Core.Application.Surfaces.SharedContext;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// One browser connection's subscription to surface context. Carries the identity the socket was
/// accepted with, so the broadcaster can decide what this connection may see without asking again.
/// </summary>
public sealed class SurfaceContextSubscription : IDisposable
{
    private readonly SurfaceContextBroadcaster _owner;
    private readonly Channel<SurfaceContextMessage> _channel;
    private bool _disposed;

    internal SurfaceContextSubscription(
        SurfaceContextBroadcaster owner,
        string workspaceKey,
        string surfaceKey,
        string? issuer,
        string? subjectId,
        IReadOnlyList<SharedContextAnchor> anchors,
        IReadOnlySet<string> requiredKeys,
        Channel<SurfaceContextMessage> channel)
    {
        _owner = owner;
        _channel = channel;
        WorkspaceKey = workspaceKey;
        SurfaceKey = surfaceKey;
        Issuer = issuer;
        SubjectId = subjectId;
        Anchors = anchors;
        RequiredKeys = requiredKeys;
    }

    public string WorkspaceKey { get; }

    public string SurfaceKey { get; }

    /// <summary>Identity provider of <see cref="SubjectId"/>. Null for an anonymous visitor.</summary>
    public string? Issuer { get; }

    /// <summary>Who is on this connection, or null when nobody was established.</summary>
    public string? SubjectId { get; }

    /// <summary>
    /// The anchors this connection holds, derived from its session at accept time (§5.5 P2).
    /// Never re-read per message and never taken from a request: a client cannot claim an anchor,
    /// because there is no syntax in which to claim one.
    /// </summary>
    public IReadOnlyList<SharedContextAnchor> Anchors { get; } = [];

    /// <summary>
    /// The context keys a visible block on this surface declared it needs (§5.5 P3). A key nobody
    /// here needs does not leave the server, whatever an anchor would theoretically permit.
    /// </summary>
    public IReadOnlySet<string> RequiredKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Values for this connection, in order. Completes when the subscription is disposed.</summary>
    public ChannelReader<SurfaceContextMessage> Messages => _channel.Reader;

    internal void TryEnqueue(SurfaceContextMessage message) => _channel.Writer.TryWrite(message);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _owner.Unsubscribe(this);
        _channel.Writer.TryComplete();
    }
}
