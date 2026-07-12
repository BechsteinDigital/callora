namespace Callora.Host.Backend.Application.Webhooks;

/// <summary>
/// Job payload for one webhook delivery attempt.
/// </summary>
public sealed record WebhookDeliveryPayload(
    Guid SubscriptionId,
    string EventName,
    string BodyJson);
