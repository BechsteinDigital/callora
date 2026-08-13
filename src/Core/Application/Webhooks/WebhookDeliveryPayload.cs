namespace Callora.Core.Application.Webhooks;

/// <summary>
/// Job payload for one webhook delivery attempt.
/// </summary>
/// <param name="SubscriptionId">Subscription the delivery belongs to.</param>
/// <param name="EventName">Platform event that triggered it.</param>
/// <param name="BodyJson">The signed body, already minimized for this subscription.</param>
/// <param name="DeliveryId">
/// Identifies this delivery across all of its attempts. It is generated once when the job is
/// enqueued, not per attempt — that is what makes it usable for deduplication on the receiving side.
/// <para>
/// Before it existed, a delivery could arrive up to five times (the job's attempt budget) with no
/// way for the receiver to tell a retry from a new event: two identical events differ in nothing a
/// receiver can see. So the duplicate deliveries were not introduced by the HTTP retry — they were
/// already possible, only harder to notice.
/// </para>
/// <para>
/// Defaulted so jobs already sitting in the queue when this shipped still deserialize. Those carry
/// an empty id and are delivered without the header rather than with an invented one: a value that
/// differs per attempt would be worse than none, because a receiver would treat every retry as new.
/// </para>
/// </param>
public sealed record WebhookDeliveryPayload(
    Guid SubscriptionId,
    string EventName,
    string BodyJson,
    Guid DeliveryId = default);
