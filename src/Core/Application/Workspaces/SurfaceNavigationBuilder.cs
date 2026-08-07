namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Baut die Navigation einer Fläche aus dem Surface-Baum (ADR-019 §5) — als reine Funktion.
/// <para>
/// Rein, weil die Regeln hier zählen und nicht die Datenbeschaffung: was mitkommt, wo der Baum
/// endet, was ein Zyklus anrichtet. Alle drei lassen sich so gegen eine Handvoll Knoten prüfen.
/// </para>
/// <para>
/// Auf Snapshots und nicht auf Entitäten: Der öffentliche Endpunkt hat nur die, und zwei
/// Verkettungswege — einer über Ids, einer über Schlüssel — wären zwei Fassungen derselben
/// Regel, von denen eine irgendwann anders entscheidet.
/// </para>
/// </summary>
public static class SurfaceNavigationBuilder
{
    /// <summary>
    /// Die Navigation unterhalb der Wurzel des angegebenen Knotens.
    /// <para>
    /// Nicht unterhalb des Knotens selbst: Wer auf <c>/portal/partner</c> steht, soll die
    /// Gliederung des Portals sehen und nicht nur das, was unter „Partner" hängt — sonst käme
    /// man von einer Unterseite nie zurück zu den Geschwistern.
    /// </para>
    /// <para>
    /// <b>Der Baum endet an der nächsten Wurzel.</b> Ein anderer Zugang desselben Workspace —
    /// der Dialer neben der Website — ist eine andere Anwendung; ihn einzublenden hieße, in
    /// einer Website auf einen Arbeitsplatz zu verlinken, für den ganz andere Leute angemeldet
    /// sind.
    /// </para>
    /// </summary>
    /// <param name="current">Der Knoten, für den gerendert wird.</param>
    /// <param name="all">Alle Surfaces des Workspaces.</param>
    /// <param name="hasLayout">Ob ein Knoten eine eigene Erlebniswelt trägt.</param>
    public static IReadOnlyList<SurfaceNavigationNode> Build(
        WorkspaceSurfaceSnapshot current,
        IReadOnlyList<WorkspaceSurfaceSnapshot> all,
        Func<WorkspaceSurfaceSnapshot, bool>? hasLayout = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(all);

        var byKey = all.ToDictionary(surface => surface.SurfaceKey, StringComparer.Ordinal);
        var root = RootOf(current, byKey);

        var childrenOf = all
            .Where(surface => !string.IsNullOrWhiteSpace(surface.ParentSurfaceKey) && surface.IsActive)
            .GroupBy(surface => surface.ParentSurfaceKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        return ChildrenOf(
            root,
            [root.PublicPathPrefix],
            childrenOf,
            hasLayout ?? (_ => false),
            new HashSet<string>(StringComparer.Ordinal));
    }

    /// <summary>
    /// Die Wurzel dieses Knotens. Bricht nach <see cref="SurfaceTree.MaxDepth"/> ab und behandelt
    /// den erreichten Knoten als Wurzel — ein Zyklus in Bestandsdaten darf den Renderpfad nicht
    /// zum Stillstand bringen.
    /// </summary>
    private static WorkspaceSurfaceSnapshot RootOf(
        WorkspaceSurfaceSnapshot node,
        IReadOnlyDictionary<string, WorkspaceSurfaceSnapshot> byKey)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { node.SurfaceKey };
        var current = node;

        while (!string.IsNullOrWhiteSpace(current.ParentSurfaceKey) &&
               seen.Add(current.ParentSurfaceKey) &&
               byKey.TryGetValue(current.ParentSurfaceKey, out var parent) &&
               seen.Count <= SurfaceTree.MaxDepth)
        {
            current = parent;
        }

        return current;
    }

    private static IReadOnlyList<SurfaceNavigationNode> ChildrenOf(
        WorkspaceSurfaceSnapshot node,
        IReadOnlyList<string?> pathFromNodeToRoot,
        IReadOnlyDictionary<string, WorkspaceSurfaceSnapshot[]> childrenOf,
        Func<WorkspaceSurfaceSnapshot, bool> hasLayout,
        HashSet<string> visited)
    {
        // Ein Knoten, der schon in dieser Kette lag, wird nicht noch einmal betreten. Ein Zyklus
        // in Bestandsdaten würde die Navigation sonst endlos aufblähen — und zwar bei jedem
        // Besucher, nicht nur bei dem, der ihn angelegt hat.
        if (!visited.Add(node.SurfaceKey) || !childrenOf.TryGetValue(node.SurfaceKey, out var children))
        {
            return [];
        }

        return children
            .OrderBy(child => child.Position)
            .ThenBy(child => child.SurfaceKey, StringComparer.Ordinal)
            .Select(child =>
            {
                var path = new List<string?> { child.PublicPathPrefix };
                path.AddRange(pathFromNodeToRoot);

                return new SurfaceNavigationNode(
                    child.SurfaceKey,
                    string.IsNullOrWhiteSpace(child.DisplayName) ? child.SurfaceKey : child.DisplayName,
                    SurfaceTree.ComposePath(path),
                    hasLayout(child),
                    ChildrenOf(child, path, childrenOf, hasLayout, visited));
            })
            .ToArray();
    }
}
