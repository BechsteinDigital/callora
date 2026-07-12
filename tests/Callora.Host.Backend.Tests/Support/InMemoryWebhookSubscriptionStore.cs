using Callora.Host.Backend.Application.Abstractions.Webhooks;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// In-memory webhook subscription store for dispatcher and handler tests.
/// </summary>
public sealed class InMemoryWebhookSubscriptionStore : IWebhookSubscriptionStore
{
    private readonly List<WebhookSubscriptionSnapshot> _subscriptions = [];

    public Task<IReadOnlyList<WebhookSubscriptionSnapshot>> ListAsync(
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSubscriptionSnapshot> result = _subscriptions
            .Where(subscription => workspaceKey is null || subscription.WorkspaceKey == workspaceKey.Trim())
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<WebhookSubscriptionSnapshot>> ListActiveForEventAsync(
        string eventName,
        string? workspaceKey,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSubscriptionSnapshot> result = _subscriptions
            .Where(subscription => subscription.IsActive)
            .Where(subscription => subscription.EventName == eventName || subscription.EventName == "*")
            .Where(subscription => subscription.WorkspaceKey is null || subscription.WorkspaceKey == workspaceKey)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<WebhookSubscriptionSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscriptions.FirstOrDefault(subscription => subscription.Id == id));

    public Task<WebhookSubscriptionSnapshot> CreateAsync(
        string? workspaceKey,
        string eventName,
        string targetUrl,
        string secret,
        CancellationToken cancellationToken = default)
    {
        var snapshot = new WebhookSubscriptionSnapshot(
            Guid.NewGuid(),
            workspaceKey,
            eventName,
            targetUrl,
            secret,
            IsActive: true,
            DateTimeOffset.UtcNow);
        _subscriptions.Add(snapshot);
        return Task.FromResult(snapshot);
    }

    public Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var index = _subscriptions.FindIndex(subscription => subscription.Id == id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _subscriptions[index] = _subscriptions[index] with { IsActive = isActive };
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_subscriptions.RemoveAll(subscription => subscription.Id == id) > 0);
}
