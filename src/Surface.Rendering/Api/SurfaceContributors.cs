using Callora.Core.Application.Workspaces;

namespace Callora.Surface.Rendering.Api;

/// <summary>
/// Wer auf DIESER Fläche beitragen darf.
/// </summary>
/// <remarks>
/// Die UI-Kette sagt, was geladen wird; sie enthält für eine Fläche ohne App jedes im Workspace
/// aktive Plugin. Das war die Ursache dafür, dass eine Inhaltsseite ohne einen einzigen Block
/// die Navigation fremder Anwendungen zeigte.
///
/// <para>
/// Gehört die Fläche einer App, hat die Kettenauflösung schon entschieden. Sonst entscheidet das
/// LAYOUT — es sagt genau, was gebraucht wird, und kann im Gegensatz zu einer gepflegten Liste
/// nicht veralten.
/// </para>
/// </remarks>
internal static class SurfaceContributors
{
    /// <summary>
    /// Die Kette, eingeschränkt auf das, was diese Fläche wirklich braucht.
    /// </summary>
    /// <remarks>
    /// Ein Block trägt seine Herkunft im Namen: <c>communication.incoming-call</c> gehört
    /// <c>communication</c>. Dieselbe Konvention, nach der die Block-Registry im Browser
    /// sortiert — eine zweite Zuordnungstabelle wäre eine zweite Wahrheit.
    ///
    /// <para>
    /// Das Theme bleibt immer drin: Es gestaltet, es rendert nicht, und eine Seite ohne Theme
    /// sähe nicht nach weniger Inhalt aus, sondern nach einem Fehler.
    /// </para>
    /// </remarks>
    public static IReadOnlyCollection<string> OnThisSurface(
        IReadOnlyCollection<string> chain,
        WorkspaceSurfaceSnapshot surface,
        IReadOnlyCollection<string> usedBlockIds)
    {
        ArgumentNullException.ThrowIfNull(chain);
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(usedBlockIds);

        // Eine Fläche mit App: Die Kette endet ohnehin bei ihr, hier gibt es nichts zu kürzen.
        if (!string.IsNullOrWhiteSpace(surface.TemplatePluginId))
        {
            return chain;
        }

        var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var blockId in usedBlockIds)
        {
            if (string.IsNullOrWhiteSpace(blockId))
            {
                continue;
            }

            needed.Add(PluginOf(blockId));
        }

        if (!string.IsNullOrWhiteSpace(surface.ThemePluginId))
        {
            needed.Add(surface.ThemePluginId);
        }

        return chain.Where(needed.Contains).ToArray();
    }

    /// <summary>
    /// Ob ein Block überhaupt ausgeliefert werden darf: nur, wenn sein Plugin in der Kette steht.
    /// </summary>
    /// <remarks>
    /// Die Kette ist bereits über <c>IPluginAvailabilityEvaluator</c> gefiltert — was hier fehlt,
    /// ist deinstalliert, deaktiviert oder für diesen Workspace nicht berechtigt.
    /// <para>
    /// Ohne diese Prüfung lieferte der Renderpfad die Insel samt <c>data-callora-props</c> weiter
    /// aus. Das JS dazu wurde zwar nie geladen — <see cref="OnThisSurface"/> schneidet die Kette
    /// auf die benutzten Präfixe —, die Insel blieb also tot. Im HTML stand aber weiter die vom
    /// Operator gespeicherte Konfiguration eines Plugins, das dieser Workspace nicht mehr haben
    /// darf.
    /// </para>
    /// </remarks>
    public static Func<string, bool> BlockIsAvailable(IReadOnlyCollection<string> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        var available = new HashSet<string>(chain, StringComparer.OrdinalIgnoreCase);
        return blockId => !string.IsNullOrWhiteSpace(blockId) && available.Contains(PluginOf(blockId));
    }

    /// <summary>
    /// Das Plugin hinter einer Block-Kennung: <c>communication.incoming-call</c> gehört
    /// <c>communication</c>. Dieselbe Konvention, nach der die Block-Registry im Browser sortiert.
    /// </summary>
    private static string PluginOf(string blockId)
    {
        var separator = blockId.IndexOf('.', StringComparison.Ordinal);
        return separator > 0 ? blockId[..separator] : blockId;
    }
}
