using Callora.Core.Application.Events;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Events;

/// <summary>
/// Default dispatcher that resolves and invokes all subscribers for one host event type.
/// </summary>
public sealed class HostApplicationEventDispatcher(
    IServiceProvider services,
    ICalloraPluginCatalog pluginCatalog,
    ILogger<HostApplicationEventDispatcher> logger,
    // Optional: Ein Host ohne Fehlerbudget rechnet nichts zu und verhält sich unverändert.
    PluginFaultRegistry? faults = null) : IHostApplicationEventDispatcher
{
    public async Task DispatchAsync<TEvent>(TEvent appEvent, CancellationToken cancellationToken = default)
        where TEvent : IHostEvent
    {
        ArgumentNullException.ThrowIfNull(appEvent);

        var hostHandlers = services
            .GetServices<IHostApplicationEventSubscriber<TEvent>>()
            .ToArray();
        // Mit Herkunft statt ohne: Dieselben Abonnenten, aber jeder weiß jetzt, wem er gehört.
        // Anders ließe sich ein dauerhaft scheiternder Plugin-Abonnent nicht zurechnen.
        var pluginHandlers = pluginCatalog
            .GetOwnedExports(typeof(IHostEventSubscriber<TEvent>))
            .Where(owned => owned.Service is IHostEventSubscriber<TEvent>)
            .Select(owned => (owned.PluginId, Subscriber: (IHostEventSubscriber<TEvent>)owned.Service))
            .ToArray();

        var handlers = hostHandlers
            .Select(x => new DispatchHandler<TEvent>(GetPriority(x), x.HandleAsync))
            .Concat(pluginHandlers.Select(x =>
                new DispatchHandler<TEvent>(x.Subscriber.Priority, x.Subscriber.HandleAsync, x.PluginId)))
            .OrderByDescending(x => x.Priority)
            .ToArray();

        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (appEvent is IHostEventPropagationState propagationState &&
                propagationState.IsPropagationStopped)
            {
                break;
            }

            try
            {
                await handler.Callback(appEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    ex,
                    "Host event subscriber callback failed for event {EventType}. Continuing with remaining subscribers.",
                    typeof(TEvent).Name);

                // Weitermachen bleibt richtig — ein Abonnent darf die übrigen nicht mitreißen.
                // Aber Weitermachen ohne Buchführung machte den Verursacher unsichtbar: Der
                // Fehler stand nur als Warnung im Log, und niemand zählte mit.
                if (handler.OwnerPluginId is { } owner)
                {
                    faults?.Record(owner, PluginFaultOrigin.Event);
                }
            }
        }
    }

    private static int GetPriority<TEvent>(IHostApplicationEventSubscriber<TEvent> handler)
        where TEvent : IHostEvent
        => handler is IHostApplicationEventSubscriberPriority prioritized
            ? prioritized.Priority
            : 0;
}
