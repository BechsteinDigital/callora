namespace Callora.Core.Application.Workspaces;

/// <summary>
/// Die Regeln des Surface-Baums (ADR-019) als reine Funktionen.
/// <para>
/// Sie stehen hier und nicht im Store, weil sie beides sind: die Bedingung beim Schreiben und
/// die Auflösung beim Lesen. Zwei Fassungen derselben Regel — eine in der Validierung, eine im
/// Renderpfad — wären zwei Gelegenheiten, sie unterschiedlich zu meinen.
/// </para>
/// </summary>
public static class SurfaceTree
{
    /// <summary>
    /// Wie tief ein Baum werden darf, bevor das Setzen abgelehnt wird.
    /// <para>
    /// Keine fachliche Grenze, sondern eine Reißleine: Die Vererbungskette wird bei jeder
    /// Anfrage durchlaufen, und eine Struktur, die irrtümlich sehr tief geworden ist, soll den
    /// Renderpfad nicht verlangsamen. Wer 32 Ebenen braucht, hat ein anderes Problem.
    /// </para>
    /// </summary>
    public const int MaxDepth = 32;

    /// <summary>
    /// Ob dieser Elternteil einen Zyklus erzeugte.
    /// <para>
    /// Geprüft beim Setzen, nicht beim Auflösen: Ein Zyklus, der erst beim Rendern auffiele,
    /// wäre eine Endlosschleife im Anfragepfad — und zwar für jeden Besucher, nicht nur für den,
    /// der ihn angelegt hat.
    /// </para>
    /// </summary>
    /// <param name="parentById">
    /// Der Elternteil jedes Knotens im Workspace. Knoten ohne Eintrag gelten als Wurzeln.
    /// </param>
    public static bool WouldCreateCycle(
        Guid nodeId,
        Guid? newParentId,
        IReadOnlyDictionary<Guid, Guid?> parentById)
    {
        ArgumentNullException.ThrowIfNull(parentById);

        // Sein eigener Elternteil ist der kürzeste Zyklus und der, den eine Kettenprüfung
        // übersähe, wenn sie erst beim Vorfahren anfinge.
        if (newParentId is null)
        {
            return false;
        }

        var current = newParentId;
        for (var step = 0; step <= MaxDepth && current is { } id; step++)
        {
            if (id == nodeId)
            {
                return true;
            }

            current = parentById.TryGetValue(id, out var parent) ? parent : null;
        }

        // Über MaxDepth hinaus ohne Ende: Entweder ein bestehender Zyklus oder eine Kette, die
        // ohnehin abgelehnt gehört. Beides ist kein gültiger Elternteil.
        return current is not null;
    }

    /// <summary>
    /// Die Kette von diesem Knoten aufwärts bis zur Wurzel — der Knoten selbst zuerst.
    /// <para>
    /// Bricht nach <see cref="MaxDepth"/> ab, statt zu hängen. Ein Zyklus in Bestandsdaten (aus
    /// einer Migration, einem direkten SQL-Eingriff) darf den Renderpfad nicht zum Stillstand
    /// bringen; er soll eine abgeschnittene Kette liefern und damit eine sichtbar falsche Seite,
    /// keine hängende Anfrage.
    /// </para>
    /// </summary>
    public static IReadOnlyList<T> AncestryOf<T>(
        T node,
        Func<T, Guid> idOf,
        Func<T, Guid?> parentIdOf,
        IReadOnlyDictionary<Guid, T> byId)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(idOf);
        ArgumentNullException.ThrowIfNull(parentIdOf);
        ArgumentNullException.ThrowIfNull(byId);

        var chain = new List<T> { node };
        var seen = new HashSet<Guid> { idOf(node) };

        var parentId = parentIdOf(node);
        while (parentId is { } id && chain.Count <= MaxDepth)
        {
            if (!seen.Add(id) || !byId.TryGetValue(id, out var parent))
            {
                break;
            }

            chain.Add(parent);
            parentId = parentIdOf(parent);
        }

        return chain;
    }

    /// <summary>
    /// Der erste gesetzte Wert entlang der Kette — die Vererbung in einer Zeile.
    /// <para>
    /// „Gesetzt" heißt nicht null und, bei Zeichenketten, nicht leer. Ein leeres Feld ist in
    /// einer Verwaltungsoberfläche dasselbe wie ein nicht ausgefülltes; es als eigenen Wert zu
    /// werten hieße, dass ein versehentlich geleertes Feld die Vererbung abschaltet.
    /// </para>
    /// </summary>
    public static TValue? Inherited<T, TValue>(IReadOnlyList<T> ancestry, Func<T, TValue?> select)
        where T : class
        where TValue : class =>
        InheritedFrom(ancestry, select) is { } node ? select(node) : null;

    /// <summary>
    /// Der Knoten, von dem ein Wert kommt — nicht der Wert selbst.
    /// <para>
    /// Für alles, was <b>zusammen</b> vererbt werden muss. Theme-Plugin und Theme-Version
    /// einzeln zu suchen ergäbe im schlimmsten Fall das Plugin des einen Vorfahren mit der
    /// Version eines anderen: eine Zuweisung, die es nie gab. Wer solche Paare hat, sucht den
    /// Knoten und liest beide Werte von dort.
    /// </para>
    /// </summary>
    public static T? InheritedFrom<T, TValue>(IReadOnlyList<T> ancestry, Func<T, TValue?> select)
        where T : class
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(ancestry);
        ArgumentNullException.ThrowIfNull(select);

        foreach (var node in ancestry)
        {
            var value = select(node);
            if (value is string text ? !string.IsNullOrWhiteSpace(text) : value is not null)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// Setzt den vollen öffentlichen Pfad aus der Kette zusammen.
    /// <para>
    /// Ein Kind trägt sein Segment, nicht den vollen Pfad: Sonst müsste beim Verschieben eines
    /// Teilbaums jeder Nachfahre umgeschrieben werden, und jeder übersehene wäre eine tote URL.
    /// </para>
    /// <para>
    /// Die Kette kommt von innen nach außen (Knoten zuerst), der Pfad entsteht andersherum.
    /// </para>
    /// </summary>
    public static string ComposePath(IReadOnlyList<string?> segmentsFromNodeToRoot)
    {
        ArgumentNullException.ThrowIfNull(segmentsFromNodeToRoot);

        var parts = segmentsFromNodeToRoot
            .Reverse()
            .Select(segment => segment?.Trim().Trim('/') ?? string.Empty)
            .Where(segment => segment.Length > 0)
            .ToArray();

        return parts.Length == 0 ? "/" : "/" + string.Join('/', parts);
    }
}
