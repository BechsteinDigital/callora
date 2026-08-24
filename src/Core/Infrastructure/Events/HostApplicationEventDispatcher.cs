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
    PluginFaultRegistry? faults = null,
    IPluginAvailabilityEvaluator? availability = null) : IHostApplicationEventDispatcher
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

        // Dieselbe Grenze wie auf dem Business-Bus: Ein Plugin, dessen Entitlement erloschen
        // ist, wird nicht mehr gefragt — und kann damit auch kein MutableHostEvent mehr
        // abbrechen. Die Herkunft steht oben ohnehin schon, für die Fehlerzurechnung;
        // gefehlt hat nur die Frage, ob der Eigentümer noch gilt. Ein Event ohne Workspace
        // bleibt ungeprüft: Verfügbarkeit wird je Workspace abgeleitet, und ein Event, das
        // keinen nennt, stellt die Frage nicht.
        if (availability is not null && pluginHandlers.Length > 0 &&
            appEvent is IBusinessEvent { WorkspaceKey: { } workspaceKey } &&
            !string.IsNullOrWhiteSpace(workspaceKey))
        {
            var pluginIds = pluginHandlers
                .Select(static x => x.PluginId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var verdicts = await availability
                .EvaluateManyAsync(pluginIds, workspaceKey, cancellationToken)
                .ConfigureAwait(false);

            pluginHandlers = pluginHandlers
                .Where(x => !verdicts.TryGetValue(x.PluginId, out var verdict) || verdict.IsAvailable)
                .ToArray();
        }

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
