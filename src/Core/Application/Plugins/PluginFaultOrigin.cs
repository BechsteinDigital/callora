namespace Callora.Core.Application.Plugins;

/// <summary>
/// Woher ein zugerechneter Plugin-Fehler kam. Der Ursprung wird mitgeführt, weil er die
/// Diagnose trägt: Fehler aus einer HTTP-Route deuten auf eine Anfrage, die ein Aufrufer
/// wiederholen kann; Fehler aus einem Hintergrund-Job oder einem Event-Handler treffen
/// niemanden, der sie meldet, und fallen deshalb sonst niemandem auf.
/// </summary>
public enum PluginFaultOrigin
{
    /// <summary>Start, Stop, Drain oder eine andere Lebenszyklus-Operation des Plugins.</summary>
    Lifecycle,

    /// <summary>Ein vom Plugin beigesteuerter HTTP-Endpunkt (Admin, Surface, öffentlich).</summary>
    HttpRoute,

    /// <summary>Ein Hintergrund-Job-Handler des Plugins.</summary>
    Job,

    /// <summary>Ein Event-Abonnent oder Flow-Handler des Plugins.</summary>
    Event,

    /// <summary>Eine WebSocket-Verbindung oder ein anderer langlebiger Kanal.</summary>
    Realtime,
}
