using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Fans server-published context values out to the browser connections that may see them.
/// In-memory runtime state: a subscription belongs to the process that accepted its socket.
/// </summary>
/// <remarks>
/// <para>
/// Each connection gets its own bounded, drop-oldest queue. That is what keeps a stalled tab from
/// slowing down the event that produced the value — the publisher writes without waiting, and a
/// connection that falls behind loses the values it could no longer have rendered in time. Context
/// is UI state, so a gap costs a stale panel until the next value, not correctness.
/// </para>
/// <para>
/// The address decides delivery, and it decides it HERE rather than in the browser. A value
/// addressed to one subject never reaches another visitor's socket, so a filter in the client
/// would be decoration — which is the point: everything that arrives in a tab is readable there.
/// </para>
/// </remarks>
public sealed class SurfaceContextBroadcaster : ISurfaceContextBroadcaster
{
    /// <summary>Values one connection may fall behind by before its oldest are dropped.</summary>
    public const int ConnectionQueueCapacity = 64;

    // Identity, not order: subscriptions come and go concurrently, each with its own scope.
    private readonly ConcurrentDictionary<SurfaceContextSubscription, byte> _subscriptions = new();

    /// <summary>
    /// Subscribes one accepted connection. The identity is the one the socket was accepted with —
    /// it is not re-read per message, so a value cannot be delivered against an identity the
    /// connection never proved.
    /// </summary>
    public SurfaceContextSubscription Subscribe(
        string workspaceKey,
        string surfaceKey,
        string? issuer,
        string? subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceKey);

        var channel = Channel.CreateBounded<SurfaceContextMessage>(
            new BoundedChannelOptions(ConnectionQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        var subscription = new SurfaceContextSubscription(
            this, workspaceKey, surfaceKey, issuer, subjectId, channel);
        _subscriptions[subscription] = 0;
        return subscription;
    }

    /// <inheritdoc />
    public void Publish(SurfaceContextAddress address, string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var message = new SurfaceContextMessage(key, value);
        foreach (var subscription in _subscriptions.Keys)
        {
            if (address.Covers(
                    subscription.WorkspaceKey,
                    subscription.SurfaceKey,
                    subscription.Issuer,
                    subscription.SubjectId))
            {
                subscription.TryEnqueue(message);
            }
        }
    }

    /// <summary>How many connections are attached. Diagnostics and tests.</summary>
    public int SubscriptionCount => _subscriptions.Count;

    internal void Unsubscribe(SurfaceContextSubscription subscription) =>
        _subscriptions.TryRemove(subscription, out _);
}
