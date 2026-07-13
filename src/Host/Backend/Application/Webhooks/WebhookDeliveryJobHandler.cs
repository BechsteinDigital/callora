using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Callora.Host.Backend.Application.Abstractions.Webhooks;
using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Host.Backend.Application.Webhooks;

/// <summary>
/// Delivers one webhook payload as signed HTTP POST. Non-success responses
/// throw so the job queue retries with backoff up to MaxAttempts.
/// </summary>
public sealed class WebhookDeliveryJobHandler(
    IWebhookSubscriptionStore store,
    IHttpClientFactory httpClientFactory,
    WebhookEgressGuard egressGuard) : IBackgroundJobHandler
{
    public const string HttpClientName = "callora-webhooks";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => WebhookDispatcher.DeliveryJobType;

    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<WebhookDeliveryPayload>(context.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Webhook delivery payload could not be parsed.");

        var subscription = await store.GetAsync(payload.SubscriptionId, cancellationToken).ConfigureAwait(false);
        if (subscription is null || !subscription.IsActive)
        {
            // Deleted or disabled while queued — nothing to deliver.
            return;
        }

        var targetUri = new Uri(subscription.TargetUrl, UriKind.Absolute);
        await egressGuard.EnsureAllowedAsync(targetUri, cancellationToken).ConfigureAwait(false);

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, targetUri)
        {
            Content = new StringContent(payload.BodyJson, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(WebhookSignature.EventHeaderName, payload.EventName);
        request.Headers.TryAddWithoutValidation(
            WebhookSignature.HeaderName,
            WebhookSignature.Compute(subscription.Secret, payload.BodyJson));

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Webhook delivery to '{subscription.TargetUrl}' failed with status {(int)response.StatusCode}.");
            }

            WebhookTelemetry.RecordDelivery(
                payload.EventName,
                "success",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch
        {
            WebhookTelemetry.RecordDelivery(
                payload.EventName,
                "failure",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }
}
