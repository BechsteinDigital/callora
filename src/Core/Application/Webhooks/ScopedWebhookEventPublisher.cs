using Callora.Host.PluginContracts.Application.Webhooks;

namespace Callora.Core.Application.Webhooks;

/// <summary>
/// Plugin-facing webhook publishing over the host dispatcher — plugins see
/// only the contract, minimization/signing/delivery stay host-side.
/// </summary>
public sealed class ScopedWebhookEventPublisher(WebhookDispatcher dispatcher) : IWebhookEventPublisher
{
    public Task PublishAsync(
        string eventName,
        string? workspaceKey,
        object payload,
        CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(eventName, workspaceKey, payload, cancellationToken);
}
