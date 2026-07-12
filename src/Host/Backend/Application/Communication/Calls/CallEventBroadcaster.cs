using System.Collections.Concurrent;

namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// Fans call events out to workspace-scoped subscribers, for example SSE
/// connections of the workspace shell.
/// </summary>
public sealed class CallEventBroadcaster
{
    private readonly ConcurrentDictionary<Guid, CallEventSubscription> _subscriptions = new();

    /// <summary>
    /// Raised for every published event regardless of workspace — host-internal
    /// consumers (webhooks, flows) attach here.
    /// </summary>
    public event Action<CallEvent>? EventPublished;

    public CallEventSubscription Subscribe(string workspaceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var subscription = new CallEventSubscription(Guid.NewGuid(), workspaceKey.Trim(), Remove);
        _subscriptions[subscription.Id] = subscription;
        return subscription;
    }

    public void Publish(CallEvent callEvent)
    {
        ArgumentNullException.ThrowIfNull(callEvent);

        EventPublished?.Invoke(callEvent);

        foreach (var subscription in _subscriptions.Values)
        {
            if (string.Equals(subscription.WorkspaceKey, callEvent.Call.WorkspaceKey, StringComparison.OrdinalIgnoreCase))
            {
                subscription.Write(callEvent);
            }
        }
    }

    private void Remove(Guid subscriptionId) => _subscriptions.TryRemove(subscriptionId, out _);
}
