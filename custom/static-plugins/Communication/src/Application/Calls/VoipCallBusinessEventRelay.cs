using Callora.Plugin.Communication.Abstractions;
using Callora.Core.Application.Events.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Forwards live call events onto the platform business-event bus (PLAT-270).
/// Flows and webhooks consume them there as generic listeners; the plugin no
/// longer relays to webhooks directly.
/// </summary>
public sealed class VoipCallBusinessEventRelay(
    ICallEventStream eventStream,
    IBusinessEventBus businessEventBus,
    ILogger? logger = null) : IDisposable
{
    public void Attach() => eventStream.EventPublished += HandleCallEvent;

    public void Dispose() => eventStream.EventPublished -= HandleCallEvent;

    private void HandleCallEvent(CallStreamEvent callEvent)
    {
        _ = PublishAsync(callEvent);
    }

    private async Task PublishAsync(CallStreamEvent callEvent)
    {
        try
        {
            await businessEventBus.PublishAsync(new CallBusinessEvent(callEvent)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Publishing business event {EventName} failed.", callEvent.Type);
        }
    }
}
