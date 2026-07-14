using Callora.Contracts.Communication;
using Callora.Host.PluginContracts.Application.Webhooks;
using Microsoft.Extensions.Logging;

namespace Callora.Plugins.Voip.Application.Calls;

/// <summary>
/// Forwards live call events into the host webhook pipeline via the
/// publisher contract; payload minimization and signing stay host-side.
/// </summary>
public sealed class VoipCallWebhookRelay(
    ICallEventStream eventStream,
    IWebhookEventPublisher webhookPublisher,
    ILogger? logger = null) : IDisposable
{
    public void Attach() => eventStream.EventPublished += HandleCallEvent;

    public void Dispose() => eventStream.EventPublished -= HandleCallEvent;

    private void HandleCallEvent(CallStreamEvent callEvent)
    {
        _ = RelayAsync(callEvent);
    }

    private async Task RelayAsync(CallStreamEvent callEvent)
    {
        try
        {
            await webhookPublisher
                .PublishAsync(callEvent.Type, callEvent.Call.WorkspaceKey, callEvent.Call)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Webhook dispatch for event {EventName} failed.", callEvent.Type);
        }
    }
}
