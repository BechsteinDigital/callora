using Callora.Core.Extensibility;

namespace Callora.Core.Application.Mcp.Contracts;

/// <summary>
/// Der Port, über den die Plugin-Laufzeit die MCP-Werkzeuge eines Plugins an- und abmeldet.
/// <para>
/// Er existiert, weil <c>RuntimePluginHost</c> in der Application-Schicht steht und die
/// umsetzende Registry in der Infrastructure: Sie hält die Sammlung, die der MCP-Server
/// ausliefert, und braucht dafür dessen SDK-Typen. Vorher stand der Infrastructure-Typ voll
/// qualifiziert im Konstruktor des Hosts — eine Abhängigkeit von innen nach außen, die
/// CODE_STRUCTURE_RULES ausschließt und die kein Test bemerkte.
/// </para>
/// <para>
/// Bewusst schmal: An- und Abmelden ist alles, was die Laufzeit von der Registry braucht. Was
/// dabei mit der ausgelieferten Sammlung geschieht — Kollisionen, das Changed-Ereignis, die
/// Autorisierung pro Aufruf —, bleibt hinter dem Port.
/// </para>
/// </summary>
[CalloraInternal("Host-internal port for MCP tool registration — not a plugin contract")]
public interface IMcpToolRegistry
{
    /// <summary>
    /// Meldet die Werkzeuge eines Plugins an. Ein erneuter Aufruf für dasselbe Plugin ersetzt
    /// dessen vorige Anmeldung, ist also wiederholbar.
    /// </summary>
    void Register(string pluginId, IMcpToolContributor contributor);

    /// <summary>
    /// Meldet die Werkzeuge eines Plugins ab. Wirkungslos, wenn es keine angemeldet hatte.
    /// </summary>
    void Deregister(string pluginId);
}
