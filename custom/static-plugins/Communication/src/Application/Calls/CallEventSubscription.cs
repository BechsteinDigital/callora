using System.Threading.Channels;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One live subscriber's view of a workspace's call transitions. Read <see cref="Events"/> until it
/// completes; dispose to unsubscribe.
/// </summary>
public sealed class CallEventSubscription : IDisposable
{
    private readonly CallEventBroadcaster _broadcaster;
    private readonly Channel<CallEventNotification> _channel;
    private bool _disposed;

    internal CallEventSubscription(
        CallEventBroadcaster broadcaster, string workspaceKey, Channel<CallEventNotification> channel)
    {
        _broadcaster = broadcaster;
        _channel = channel;
        WorkspaceKey = workspaceKey;
    }

    /// <summary>The workspace this subscription is filtered to.</summary>
    public string WorkspaceKey { get; }

    /// <summary>The subscriber's queue. Completes when the subscription is disposed.</summary>
    public ChannelReader<CallEventNotification> Events => _channel.Reader;

    /// <summary>
    /// Enqueues a transition when it belongs to this subscription's workspace. Never blocks: a full
    /// queue drops its oldest entry.
    /// </summary>
    internal void TryEnqueue(CallEventNotification notification)
    {
        if (_disposed || !string.Equals(notification.WorkspaceKey, WorkspaceKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _channel.Writer.TryWrite(notification);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _broadcaster.Unsubscribe(this);
        _channel.Writer.TryComplete();
    }
}
