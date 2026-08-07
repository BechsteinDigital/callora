namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Was ein Löschversuch ergab.
/// <para>
/// Drei Ausgänge, nicht zwei. „Gibt es nicht" und „hat noch Unterelemente" sind verschiedene
/// Antworten — 404 gegen 409 —, und nur der Store kann sie unterscheiden. Mit einem
/// <c>bool</c> wäre die zweite als „nicht gefunden" erschienen, und der Operator hätte einen
/// Knoten gesucht, der die ganze Zeit vor ihm stand.
/// </para>
/// </summary>
public enum SurfaceDeleteResult
{
    /// <summary>Die Surface wurde entfernt.</summary>
    Deleted,

    /// <summary>Es gibt sie in diesem Workspace nicht.</summary>
    NotFound,

    /// <summary>
    /// Sie hat Kind-Knoten. Was mit ihnen geschehen soll, ist eine Entscheidung des Operators
    /// (ADR-019 §7): sie an den Großelternknoten zu hängen ändert stillschweigend URLs, sie
    /// mitzulöschen verliert Layouts. Bis er sie trifft, passiert nichts.
    /// </summary>
    HasChildren,
}
