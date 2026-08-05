using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Fans call transitions out to the workspace's live subscribers. In-memory runtime state: a
/// subscriber belongs to the process that accepted its socket.
/// </summary>
/// <remarks>
/// Each subscriber gets its own bounded, drop-oldest queue. That is what keeps a stalled browser tab
/// from ever slowing a call down — the publisher writes without waiting, and a subscriber that falls
/// behind loses the events it could no longer have rendered in time anyway. The current state is
/// always one <c>GET calls/active</c> away, so a gap costs a refresh, not correctness.
/// </remarks>
public sealed class CallEventBroadcaster : ICallEventPublisher
{
    /// <summary>Events one subscriber may fall behind by before its oldest are dropped.</summary>
    public const int SubscriberQueueCapacity = 64;

    // Identity, not order: subscriptions are added and removed concurrently, and each carries its own
    // workspace filter.
    private readonly ConcurrentDictionary<CallEventSubscription, byte> _subscribers = new();

    /// <summary>
    /// Subscribes to the workspace's call transitions. Disposing the subscription unsubscribes and
    /// completes its reader.
    /// </summary>
    public CallEventSubscription Subscribe(string workspaceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var channel = Channel.CreateBounded<CallEventNotification>(new BoundedChannelOptions(SubscriberQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var subscription = new CallEventSubscription(this, workspaceKey, channel);
        _subscribers[subscription] = 0;
        return subscription;
    }

    /// <inheritdoc />
    public void Publish(CallEventNotification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        foreach (var subscriber in _subscribers.Keys)
        {
            subscriber.TryEnqueue(notification);
        }
    }

    /// <summary>How many subscribers are currently attached. Diagnostics and tests.</summary>
    public int SubscriberCount => _subscribers.Count;

    internal void Unsubscribe(CallEventSubscription subscription) => _subscribers.TryRemove(subscription, out _);
}
