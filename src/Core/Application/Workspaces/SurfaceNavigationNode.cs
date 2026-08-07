namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Ein Eintrag in der Navigation einer Fläche (ADR-019 §5).
/// <para>
/// Die Navigation eines Knotens sind seine Kinder — bis zur nächsten Wurzel und nicht darüber
/// hinaus. Damit stehen Website und Dialer nie in derselben Navigation, obwohl beide Surfaces
/// sind: Sie sind verschiedene Anwendungen, und eine Anwendung endet an ihrer Wurzel.
/// </para>
/// </summary>
/// <param name="SurfaceKey">Der Knoten, technisch.</param>
/// <param name="Label">Was angezeigt wird.</param>
/// <param name="Path">
/// Der volle öffentliche Pfad, zusammengesetzt aus der Kette — das, was in ein <c>href</c>
/// gehört. Das gespeicherte Segment allein wäre keine erreichbare Adresse.
/// </param>
/// <param name="HasLayout">
/// Ob dieser Knoten eine eigene Erlebniswelt hat. Ein Knoten ohne bleibt navigierbar — er ist
/// dann eine Gliederungsebene, kein Fehler —, aber eine Oberfläche darf ihn anders darstellen
/// als ein Ziel mit Inhalt.
/// </param>
/// <param name="Children">Die Kinder dieses Knotens, in ihrer Reihenfolge.</param>
public sealed record SurfaceNavigationNode(
    string SurfaceKey,
    string Label,
    string Path,
    bool HasLayout,
    IReadOnlyList<SurfaceNavigationNode> Children);
