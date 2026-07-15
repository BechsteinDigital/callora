using Callora.Core.Application.Webhooks;
using Callora.Core.Application.Events.Contracts;

namespace Callora.Core.Application.Events.Business;

/// <summary>
/// Routes every business event to the webhook pipeline (PLAT-270): matching
/// subscriptions receive the (minimized) event data. Replaces per-subsystem
/// webhook relays with one generic subscriber.
/// </summary>
public sealed class WebhookBusinessEventListener(WebhookDispatcher dispatcher) : IBusinessEventListener
{
    public int Priority => 0;

    public Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(
            businessEvent.EventName,
            businessEvent.WorkspaceKey,
            businessEvent.ToEventData(),
            cancellationToken);
}
