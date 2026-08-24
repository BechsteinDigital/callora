using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Events.Business;

/// <summary>
/// Fans one business event out to every listener — host rails resolved from
/// DI plus plugin-exported listeners — ordered by priority, isolating
/// failures so one bad listener does not stop the others (PLAT-270).
/// </summary>
public sealed class BusinessEventBus(
    IServiceProvider services,
    ICalloraPluginCatalog pluginCatalog,
    ILogger<BusinessEventBus> logger,
    // Eine Scope-Fabrik statt des Prüfers selbst: Der Bus ist ein Singleton, der Prüfer
    // scoped. Direkt injiziert hinge er an einem Wurzel-Scope, dessen DbContext niemand
    // schließt — und aus einem Job heraus gäbe es gar keinen Scope, aus dem er käme.
    IServiceScopeFactory? scopeFactory = null) : IBusinessEventBus
{
    public async Task PublishAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(businessEvent);

        // Resolved with provenance rather than through MergeWithHost, which drops it: the
        // gate below has to tell a host rail from a plugin listener, and only a plugin can
        // lose an entitlement.
        var pluginListeners = pluginCatalog
            .GetOwnedExports(typeof(IBusinessEventListener))
            .Where(static owned => owned.Service is IBusinessEventListener)
            .Select(static owned => (owned.PluginId, Listener: (IBusinessEventListener)owned.Service))
            .ToArray();

        // A revoked entitlement used to darken only the plugin's HTTP routes. For events that
        // was the worse half: MutableBusinessEvent exists so a listener can VETO a host
        // operation, so a plugin the workspace no longer holds kept the power to block
        // business operations — for a customer who could not see why they failed. An
        // unavailable plugin is not consulted, so it cannot cancel and cannot stop the fan-out.
        if (scopeFactory is not null && pluginListeners.Length > 0)
        {
            using var scope = scopeFactory.CreateScope();
            var availability = scope.ServiceProvider.GetService<IPluginAvailabilityEvaluator>();
            if (availability is not null)
            {
                pluginListeners = await WithdrawUnavailablePluginsAsync(
                        availability,
                        pluginListeners,
                        businessEvent.EventName,
                        businessEvent.WorkspaceKey,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Host rails first, so a plugin listener of equal priority still runs after them —
        // the ordering MergeWithHost produced, preserved by the stable sort.
        var listeners = services
            .GetServices<IBusinessEventListener>()
            .Concat(pluginListeners.Select(static x => x.Listener))
            .OrderByDescending(static listener => listener.Priority)
            .ToArray();

        await FanOutAsync(listeners, businessEvent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the listeners of every plugin that is not effectively available in the event's
    /// workspace, so an unavailable plugin is never consulted — and therefore can neither
    /// veto the operation nor stop the fan-out.
    /// </summary>
    private async Task<(string PluginId, IBusinessEventListener Listener)[]> WithdrawUnavailablePluginsAsync(
        IPluginAvailabilityEvaluator availability,
        (string PluginId, IBusinessEventListener Listener)[] pluginListeners,
        string eventName,
        string? workspaceKey,
        CancellationToken cancellationToken)
    {
        var pluginIds = pluginListeners
            .Select(static x => x.PluginId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        // Ohne Workspace ist es plattformweite Zustellung — dieselbe Grenze, andere Frage.
        var verdicts = string.IsNullOrWhiteSpace(workspaceKey)
            ? await EvaluatePlatformManyAsync(availability, pluginIds, cancellationToken).ConfigureAwait(false)
            : await availability.EvaluateManyAsync(pluginIds, workspaceKey, cancellationToken).ConfigureAwait(false);

        foreach (var pluginId in pluginIds)
        {
            if (verdicts.TryGetValue(pluginId, out var verdict) && !verdict.IsAvailable)
            {
                logger.LogDebug(
                    "Business event {EventName} withheld from plugin {PluginId}: unavailable in workspace {WorkspaceKey} ({UnmetFactors}).",
                    eventName,
                    pluginId,
                    workspaceKey ?? "<platform>",
                    string.Join(", ", verdict.UnmetFactors));
            }
        }

        return pluginListeners
            .Where(x => !verdicts.TryGetValue(x.PluginId, out var verdict) || verdict.IsAvailable)
            .ToArray();
    }

    private static async Task<IReadOnlyDictionary<string, PluginAvailability>> EvaluatePlatformManyAsync(
        IPluginAvailabilityEvaluator availability,
        IReadOnlyCollection<string> pluginIds,
        CancellationToken cancellationToken)
    {
        var verdicts = new Dictionary<string, PluginAvailability>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginId in pluginIds)
        {
            verdicts[pluginId] = await availability
                .EvaluatePlatformAsync(pluginId, cancellationToken)
                .ConfigureAwait(false);
        }

        return verdicts;
    }

    private async Task FanOutAsync(
        IBusinessEventListener[] listeners,
        IBusinessEvent businessEvent,
        CancellationToken cancellationToken)
    {
        foreach (var listener in listeners)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A mutable business event (MutableBusinessEvent) can stop the fan-out; read-only
            // events never implement this, so the check is inert for them.
            if (businessEvent is IHostEventPropagationState { IsPropagationStopped: true })
            {
                break;
            }

            try
            {
                await listener.OnBusinessEventAsync(businessEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    exception,
                    "Business event listener {Listener} failed for event {EventName}.",
                    listener.GetType().Name,
                    businessEvent.EventName);
            }
        }
    }
}
