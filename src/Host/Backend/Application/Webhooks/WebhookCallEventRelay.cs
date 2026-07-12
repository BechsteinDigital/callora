using Callora.Host.Backend.Application.Communication.Calls;

namespace Callora.Host.Backend.Application.Webhooks;

/// <summary>
/// Forwards live call events into the webhook dispatcher.
/// </summary>
public sealed class WebhookCallEventRelay(
    CallEventBroadcaster broadcaster,
    WebhookDispatcher dispatcher) : IHostedService
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
        _ = dispatcher.DispatchAsync(callEvent.Type, callEvent.Call.WorkspaceKey, callEvent.Call);
    }
}
