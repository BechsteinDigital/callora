using System.Text.Json;
using Callora.Core.Application.Webhooks;
using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Core.Application.Webhooks;

/// <summary>
/// Matches platform events against webhook subscriptions and enqueues one
/// durable delivery job per match. Singleton — resolves the scoped store per
/// dispatch.
/// </summary>
public sealed class WebhookDispatcher(
    IServiceScopeFactory scopeFactory,
    IBackgroundJobQueue jobQueue,
    ILogger<WebhookDispatcher> logger)
{
    public const string DeliveryJobType = "webhook.deliver";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task DispatchAsync(
        string eventName,
        string? workspaceKey,
        object payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        IReadOnlyList<WebhookSubscriptionSnapshot> subscriptions;
        using (var scope = scopeFactory.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IWebhookSubscriptionStore>();
            subscriptions = await store
                .ListActiveForEventAsync(eventName, workspaceKey, cancellationToken)
                .ConfigureAwait(false);
        }

        if (subscriptions.Count == 0)
        {
            return;
        }

        var body = JsonSerializer.Serialize(new
        {
            @event = eventName,
            workspaceKey,
            occurredAtUtc = DateTimeOffset.UtcNow,
            data = payload
        }, JsonOptions);

        // Datenminimierung: maskierte Variante ist der Default; Abos mit
        // explizitem Opt-in erhalten den vollen Payload (PLAT-244).
        string? minimizedBody = null;

        foreach (var subscription in subscriptions)
        {
            try
            {
                var subscriptionBody = subscription.IncludeSensitiveData
                    ? body
                    : minimizedBody ??= WebhookPayloadMinimizer.Minimize(body);
                var jobPayload = JsonSerializer.Serialize(
                    new WebhookDeliveryPayload(subscription.Id, eventName, subscriptionBody), JsonOptions);
                await jobQueue.EnqueueAsync(
                        new BackgroundJobRequest(
                            DeliveryJobType,
                            jobPayload,
                            MaxAttempts: 5,
                            WorkspaceKey: subscription.WorkspaceKey),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Enqueueing webhook delivery for subscription {SubscriptionId} on event {EventName} failed.",
                    subscription.Id,
                    eventName);
            }
        }
    }
}
