using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Zählt zugerechnete Plugin-Fehler in einem gleitenden Fenster und nimmt einem Plugin die
/// Verfügbarkeit, sobald es sein Budget überschreitet.
/// </summary>
/// <remarks>
/// <para>
/// Die Lücke, die das schließt: Ein Plugin, das beim Aktivieren scheitert, wird
/// <see cref="Contracts.HostPluginState.Faulted"/> und fällt über den Faktor
/// <see cref="PluginAvailabilityFactor.RuntimeHealthy"/> aus der Verfügbarkeit. Ein Plugin, das
/// AKTIV ist und bei jeder zweiten Anfrage wirft, tut das nicht: Es blieb unbegrenzt verfügbar,
/// riss jede Anfrage mit und fiel erst auf, wenn jemand die Logs las. In einem Prozess, den sich
/// mehrere Plugins teilen, trägt diesen Schaden nicht der Verursacher.
/// </para>
/// <para>
/// Bewusst weich und ohne neuen Zustand — dasselbe Muster wie bei den Runtime-Capabilities: Das
/// Plugin bleibt aktiv, seine gewünschte Aktivierung bleibt unangetastet, es gilt nur für die
/// Dauer des Fensters nicht als verfügbar. Eine harte Deaktivierung wäre ein Schreibzugriff auf
/// den Wunsch des Betreibers als Antwort auf eine Störung, und sie müsste von Hand
/// zurückgenommen werden.
/// </para>
/// <para>
/// Selbstheilung ist Teil des Entwurfs: Läuft das Fenster ohne neue Fehler leer, ist das Plugin
/// wieder verfügbar. Ein Budget ohne Rückweg wäre eine stille Deaktivierung — der Betreiber
/// sucht dann nach einem Schalter, den niemand umgelegt hat.
/// </para>
/// <para>
/// Was hier NICHT steht, ist Speicherzurechnung. .NET misst keinen Speicher je
/// <c>AssemblyLoadContext</c>; ein Zähler dafür wäre erfunden. Was der Host über den Speicher
/// eines Plugins tatsächlich weiß, steht in <see cref="AssemblyLoadContextUnload"/>: ob sein
/// Kontext nach dem Entladen verschwindet.
/// </para>
/// </remarks>
public sealed class PluginFaultRegistry
{
    private readonly int _threshold;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PluginFaultRegistry>? _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, List<PluginFaultEntry>> _faults =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _reported = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Erzeugt die Registry.
    /// </summary>
    /// <param name="threshold">
    /// Zahl der Fehler im Fenster, ab der ein Plugin sein Budget überschreitet. Ein Wert von 0
    /// oder kleiner schaltet das Budget ab — dann wird nur gezählt, nie entzogen.
    /// </param>
    /// <param name="window">Das gleitende Fenster, über das gezählt wird.</param>
    /// <param name="timeProvider">Zeitquelle; in Tests eine feste.</param>
    /// <param name="logger">
    /// Optional. Ohne ihn schlägt das Budget still zu — und eine stille Entzugsentscheidung ist
    /// die, nach der ein Betreiber am längsten sucht.
    /// </param>
    public PluginFaultRegistry(
        int threshold,
        TimeSpan window,
        TimeProvider timeProvider,
        ILogger<PluginFaultRegistry>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _threshold = threshold;
        _window = window;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Wird einmal je Übergang ins Überschreiten ausgelöst — nicht bei jedem weiteren Fehler.
    /// Heilt das Plugin und überschreitet erneut, meldet sich das Ereignis wieder.
    /// </summary>
    public event Action<PluginFaultBudgetExceeded>? BudgetExceeded;

    /// <summary>
    /// Rechnet einen Fehler dem Plugin zu.
    /// </summary>
    /// <param name="pluginId">Das verursachende Plugin.</param>
    /// <param name="origin">Woher der Fehler kam.</param>
    public void Record(string pluginId, PluginFaultOrigin origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        PluginFaultBudgetExceeded? report = null;
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (!_faults.TryGetValue(pluginId, out var entries))
            {
                entries = [];
                _faults[pluginId] = entries;
            }

            Prune(pluginId, entries, now);
            entries.Add(new PluginFaultEntry(now, origin));

            if (_threshold <= 0 || entries.Count < _threshold || !_reported.Add(pluginId))
            {
                return;
            }

            report = new PluginFaultBudgetExceeded(
                pluginId,
                entries.Count,
                _window,
                [.. entries
                    .GroupBy(static entry => entry.Origin)
                    .OrderByDescending(static group => group.Count())
                    .Select(static group => group.Key)]);
        }

        _logger?.LogWarning(
            "Plugin {PluginId} exceeded its fault budget ({FaultCount} faults within {Window}); "
            + "it is treated as unavailable until the window clears. Origins: {Origins}.",
            report.PluginId,
            report.FaultCount,
            report.Window,
            string.Join(", ", report.Origins));

        // Außerhalb des Locks: Ein Abonnent, der seinerseits die Registry befragt, liefe sonst
        // in einen Deadlock — dieselbe Regel, nach der die Capability-Registry ihre Flips meldet.
        BudgetExceeded?.Invoke(report);
    }

    /// <summary>
    /// Ob das Plugin derzeit innerhalb seines Budgets liegt. Ein unbekanntes Plugin liegt es.
    /// </summary>
    public bool IsWithinBudget(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        if (_threshold <= 0)
        {
            return true;
        }

        lock (_gate)
        {
            if (!_faults.TryGetValue(pluginId, out var entries))
            {
                return true;
            }

            Prune(pluginId, entries, _timeProvider.GetUtcNow());
            return entries.Count < _threshold;
        }
    }

    /// <summary>
    /// Verwirft die Fehlerhistorie eines Plugins. Gehört an jede Reaktivierung: Ein Budget aus
    /// der vorigen Fassung schlüge sonst sofort wieder zu.
    /// </summary>
    public void Clear(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        lock (_gate)
        {
            _faults.Remove(pluginId);
            _reported.Remove(pluginId);
        }
    }

    /// <summary>
    /// Wirft Einträge aus dem Fenster und führt den Melde-Zustand nach.
    /// </summary>
    /// <remarks>
    /// Das Nachführen steht hier und nicht in <see cref="IsWithinBudget"/>, wo es zuerst stand.
    /// Dort hing die Heilung daran, dass jemand FRAGT: Ein Host, der nur <see cref="Record"/>
    /// aufruft — weil gerade niemand die Verfügbarkeit abfragt —, behielt den Melde-Zustand für
    /// immer, und die nächste Überschreitung blieb still. Ein Zustandsübergang darf nicht davon
    /// abhängen, wer zusieht.
    /// </remarks>
    private void Prune(string pluginId, List<PluginFaultEntry> entries, DateTimeOffset now)
    {
        var cutoff = now - _window;
        entries.RemoveAll(entry => entry.At <= cutoff);
        if (entries.Count < _threshold)
        {
            _reported.Remove(pluginId);
        }
    }
}
