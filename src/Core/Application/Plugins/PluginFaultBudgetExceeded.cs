namespace Callora.Core.Application.Plugins;

/// <summary>
/// Meldung, dass ein Plugin sein Fehlerbudget überschritten hat und bis zum Ablauf des
/// Fensters nicht mehr als verfügbar gilt.
/// </summary>
/// <param name="PluginId">Das betroffene Plugin.</param>
/// <param name="FaultCount">Zahl der Fehler im Fenster zum Zeitpunkt der Überschreitung.</param>
/// <param name="Window">Das Fenster, über das gezählt wurde.</param>
/// <param name="Origins">
/// Die beteiligten Ursprünge, absteigend nach Häufigkeit. Sie beantworten die erste Frage
/// eines Betreibers — kommt das aus den Anfragen oder aus dem Hintergrund? —, ohne dass er
/// dafür Logs korrelieren muss.
/// </param>
public sealed record PluginFaultBudgetExceeded(
    string PluginId,
    int FaultCount,
    TimeSpan Window,
    IReadOnlyList<PluginFaultOrigin> Origins);
