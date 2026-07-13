using System.Diagnostics.Metrics;

namespace Callora.Host.Backend.Application.Webhooks;

/// <summary>
/// Metrics for outbound webhook deliveries (PLAT-230).
/// </summary>
public static class WebhookTelemetry
{
    public const string MeterName = "Callora.Host.Backend.Webhooks";

    private static readonly Meter WebhookMeter = new(MeterName);

    private static readonly Counter<long> Delivered = WebhookMeter.CreateCounter<long>(
        "callora.webhooks.delivered",
        unit: "delivery",
        description: "Webhook delivery attempts by outcome.");

    private static readonly Histogram<double> DeliveryDurationMs = WebhookMeter.CreateHistogram<double>(
        "callora.webhooks.delivery.duration",
        unit: "ms",
        description: "Duration of webhook delivery attempts.");

    public static void RecordDelivery(string eventName, string outcome, double durationMs)
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("webhook.event", eventName),
            new("webhook.outcome", outcome)
        };

        Delivered.Add(1, tags);
        DeliveryDurationMs.Record(durationMs, tags);
    }
}
