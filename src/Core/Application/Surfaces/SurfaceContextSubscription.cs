using System.Threading.Channels;

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
        Channel<SurfaceContextMessage> channel)
    {
        _owner = owner;
        _channel = channel;
        WorkspaceKey = workspaceKey;
        SurfaceKey = surfaceKey;
        Issuer = issuer;
        SubjectId = subjectId;
    }

    public string WorkspaceKey { get; }

    public string SurfaceKey { get; }

    /// <summary>Identity provider of <see cref="SubjectId"/>. Null for an anonymous visitor.</summary>
    public string? Issuer { get; }

    /// <summary>Who is on this connection, or null when nobody was established.</summary>
    public string? SubjectId { get; }

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
