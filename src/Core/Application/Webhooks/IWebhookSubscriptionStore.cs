namespace Callora.Core.Application.Webhooks;

public interface IWebhookSubscriptionStore
{
    Task<IReadOnlyList<WebhookSubscriptionSnapshot>> ListAsync(
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>Active subscriptions matching the event (exact or "*") and workspace.</summary>
    Task<IReadOnlyList<WebhookSubscriptionSnapshot>> ListActiveForEventAsync(
        string eventName,
        string? workspaceKey,
        CancellationToken cancellationToken = default);

    Task<WebhookSubscriptionSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WebhookSubscriptionSnapshot> CreateAsync(
        string? workspaceKey,
        string eventName,
        string targetUrl,
        string secret,
        bool includeSensitiveData = false,
        CancellationToken cancellationToken = default);

    Task<bool> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
