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

            var separator = blockId.IndexOf('.', StringComparison.Ordinal);
            needed.Add(separator > 0 ? blockId[..separator] : blockId);
        }

        if (!string.IsNullOrWhiteSpace(surface.ThemePluginId))
        {
            needed.Add(surface.ThemePluginId);
        }

        return chain.Where(needed.Contains).ToArray();
    }
}
