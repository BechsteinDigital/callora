using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Webhooks;
using System.Text;
using System.Text.Json;

namespace Callora.Core.Application.Flows.Actions;

/// <summary>
/// Posts the event payload to an ad-hoc URL ("url", optional "secret") —
/// for flow-specific integrations without a standing subscription.
/// </summary>
public sealed class WebhookSendActionHandler(
    IHttpClientFactory httpClientFactory,
    WebhookEgressGuard egressGuard) : IFlowActionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Type => "webhook.send";

    public async Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!parameters.TryGetValue("url", out var url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var target) ||
            (target.Scheme != Uri.UriSchemeHttps && target.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("webhook.send requires an absolute http(s) 'url' parameter.");
        }

        await egressGuard.EnsureAllowedAsync(target, cancellationToken).ConfigureAwait(false);

        var body = JsonSerializer.Serialize(new
        {
            @event = context.EventName,
            workspaceKey = context.WorkspaceKey,
            data = context.Data
        }, JsonOptions);

        var client = httpClientFactory.CreateClient(WebhookDeliveryJobHandler.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, target)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation(WebhookSignature.EventHeaderName, context.EventName);
        if (parameters.TryGetValue("secret", out var secret) && !string.IsNullOrWhiteSpace(secret))
        {
            request.Headers.TryAddWithoutValidation(
                WebhookSignature.HeaderName,
                WebhookSignature.Compute(secret, body));
        }

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"webhook.send to '{target}' failed with status {(int)response.StatusCode}.");
        }
    }
}
