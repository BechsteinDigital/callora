using Callora.Host.Backend.Application.Communication.Calls;

namespace Callora.Host.Backend.Application.Webhooks;

/// <summary>
/// Forwards live call events into the webhook dispatcher.
/// </summary>
public sealed class WebhookCallEventRelay(
    CallEventBroadcaster broadcaster,
    WebhookDispatcher dispatcher,
    ILogger<WebhookCallEventRelay> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        broadcaster.EventPublished += HandleCallEvent;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        broadcaster.EventPublished -= HandleCallEvent;
        return Task.CompletedTask;
    }

    private void HandleCallEvent(CallEvent callEvent)
    {
        _ = RelayAsync(callEvent);
    }

    private async Task RelayAsync(CallEvent callEvent)
    {
        try
        {
            await dispatcher
                .DispatchAsync(callEvent.Type, callEvent.Call.WorkspaceKey, callEvent.Call)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook dispatch for event {EventName} failed.", callEvent.Type);
        }
    }
}
