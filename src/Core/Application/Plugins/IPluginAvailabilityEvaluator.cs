using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Derives whether a plugin is effectively available in a workspace (REV2 §3.2).
/// Serving paths depend on this abstraction so the single canonical derivation
/// (<see cref="PluginAvailability.From(PluginPlatformInputs, PluginWorkspaceInputs)"/>) is reused at runtime, never
/// re-implemented per consumer.
/// </summary>
[CalloraInternal("Availability enforcement gate — not a plugin contract (REV2 §7.2)")]
public interface IPluginAvailabilityEvaluator
{
    Task<PluginAvailability> EvaluateAsync(
        string pluginId,
        string workspaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Derives whether a plugin may do <b>any</b> work on this host, for the entry points
    /// that name no workspace: platform-wide jobs and events, plugin-wide routes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A different question from <see cref="EvaluateAsync"/>, not a weaker one. It combines
    /// only the four factors that hold host-wide (<see cref="PluginPlatformInputs"/>) — which
    /// are precisely the ones that must hold in every workspace — so a plugin activated in no
    /// workspace at all may still work platform-wide.
    /// </para>
    /// <para>
    /// The default implementation refuses rather than guesses: an evaluator that has not
    /// implemented the platform question must not answer it with a workspace answer, and
    /// returning "available" would open exactly the gate this abstraction exists to close.
    /// </para>
    /// </remarks>
    Task<PluginAvailability> EvaluatePlatformAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginAvailability(
            IsAvailable: false,
            UnmetFactors: [PluginAvailabilityFactor.BundledOrInstalled]));

    /// <summary>
    /// Wertet mehrere Plugins desselben Workspaces in einem Zug aus.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Existiert, weil die Einzelauswertung in einer Schleife nicht harmlos ist: Von ihren
    /// Faktoren hängen die meisten am WORKSPACE und nicht am Plugin — der Workspace selbst, seine
    /// aktivierten Plugins, die Installationsliste. Je Plugin gefragt, wird dieselbe Antwort
    /// so oft geholt, wie es Plugins gibt, und das mitten im Aufbau einer Seite.
    /// </para>
    /// <para>
    /// Die Ableitung bleibt dieselbe (<see cref="PluginAvailability.From(PluginPlatformInputs, PluginWorkspaceInputs)"/>). Was sich ändert,
    /// ist allein, wie oft ihre Eingaben beschafft werden.
    /// </para>
    /// </remarks>
    /// <returns>Verfügbarkeit je Plugin-Id, ohne Beachtung der Groß-/Kleinschreibung.</returns>
    /// <remarks>
    /// Die Standardimplementierung ruft schlicht <see cref="EvaluateAsync"/> in einer Schleife.
    /// Sie ist für Fakes und minimale Implementierungen gedacht, die ohnehin keine Datenbank
    /// anfassen — <b>nicht</b> als Vorbild: Wer echte Daten beschafft, gewinnt hier nichts und
    /// sollte überschreiben, so wie <see cref="PluginAvailabilityEvaluator"/> es tut.
    /// </remarks>
    async Task<IReadOnlyDictionary<string, PluginAvailability>> EvaluateManyAsync(
        IReadOnlyCollection<string> pluginIds,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginIds);

        var result = new Dictionary<string, PluginAvailability>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginId in pluginIds)
        {
            if (result.ContainsKey(pluginId))
            {
                continue;
            }

            result[pluginId] = await EvaluateAsync(pluginId, workspaceKey, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
