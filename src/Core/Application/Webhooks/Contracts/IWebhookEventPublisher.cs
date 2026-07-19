using Callora.Core.Extensibility;

namespace Callora.Core.Application.Webhooks.Contracts;

/// <summary>
/// Host-provided webhook publishing for plugins: matches the event against
/// the workspace's webhook subscriptions and enqueues durable deliveries.
/// Payload minimization and signing happen host-side.
/// </summary>
[CalloraExtensible(ExtensionPointMode.Decoratable, "Decorate via IServiceDecorator<IWebhookEventPublisher> to customize webhook delivery (REV2 §4.1)")]
public interface IWebhookEventPublisher
{
    /// <summary>Publishes one business event to all matching subscriptions.</summary>
    Task PublishAsync(
        string eventName,
        string? workspaceKey,
        object payload,
        CancellationToken cancellationToken = default);
}
