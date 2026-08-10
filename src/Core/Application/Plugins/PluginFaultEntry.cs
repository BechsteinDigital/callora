namespace Callora.Core.Application.Plugins;

/// <summary>
/// Ein einzelner zugerechneter Fehler mit seinem Zeitpunkt — der Eintrag, über den das
/// gleitende Fenster der <see cref="PluginFaultRegistry"/> rechnet.
/// </summary>
/// <param name="At">Wann der Fehler auftrat (UTC, aus der Zeitquelle der Registry).</param>
/// <param name="Origin">Woher er kam.</param>
internal readonly record struct PluginFaultEntry(DateTimeOffset At, PluginFaultOrigin Origin);
