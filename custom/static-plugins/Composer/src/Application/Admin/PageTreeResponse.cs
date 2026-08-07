namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Eine Seite im Editor: ein Surface-Knoten mit dem Layout, das ihn rendert.
/// <para>
/// Der Editor braucht beides zusammen, und zwar in EINER Antwort. Aus zwei Quellen
/// zusammengesetzt — Surfaces hier, Layouts dort — wäre die Liste im Moment zwischen den
/// beiden Aufrufen inkonsistent, und ein gerade angelegter Knoten erschiene ohne sein Layout.
/// </para>
/// </summary>
/// <param name="SurfaceKey">Die Fläche.</param>
/// <param name="Label">Was im Baum steht.</param>
/// <param name="ParentSurfaceKey">Der Elternknoten, oder null für eine Anwendungswurzel.</param>
/// <param name="Position">Reihenfolge unter Geschwistern.</param>
/// <param name="LayoutKey">
/// Das Layout dieser Fläche, oder null. Ein Knoten ohne Layout ist eine Gliederungsebene und
/// kein Fehler — der Editor bietet dort an, eines anzulegen.
/// </param>
/// <param name="HasPublishedVersion">Ob Besucher es sehen.</param>
public sealed record PageTreeResponse(
    string SurfaceKey,
    string Label,
    string? ParentSurfaceKey,
    int Position,
    string? LayoutKey,
    bool HasPublishedVersion);
