using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Ob eine aus dem Cookie gelesene Sitzung für eine bestimmte Fläche gilt.
/// </summary>
/// <remarks>
/// <para>
/// Der Renderpfad braucht das nicht: Dort wird der Aufrufer aus derselben Route aufgelöst, die
/// auch die Fläche bestimmt — er kann gar nicht abweichen. Seams, die den Aufrufer aus dem
/// Cookie lesen (WebSocket-Upgrade, ADR-017 §9), bekommen dagegen eine Sitzung, die für
/// irgendeine Fläche ausgestellt wurde, und müssen selbst fragen, ob es ihre ist.
/// </para>
/// <para>
/// Der Befund, der zu dieser Regel führte: Der Kontext-Socket akzeptierte die auf Fläche A
/// ausgestellte Sitzung als Anmeldung an Fläche B — und übertrug danach den Kontext von B.
/// Die Frage war nicht falsch beantwortet, sie wurde gar nicht gestellt, weil der Auflöser nur
/// den Aufrufer zurückgab und den Scope verwarf.
/// </para>
/// </remarks>
[CalloraInternal("Reichweite der Flächen-Sitzung — Durchsetzung, kein Plugin-Vertrag")]
public static class SurfaceSessionScope
{
    /// <summary>
    /// Ob <paramref name="context"/> für die durch <paramref name="workspaceKey"/> und
    /// <paramref name="surfaceKey"/> bezeichnete Fläche ausgestellt wurde.
    /// </summary>
    /// <remarks>
    /// Verglichen wird beides. Der Workspace allein genügt nicht: Zwei Flächen desselben
    /// Workspaces sind verschiedene Zugänge (ADR-019) — ein öffentliches Portal und ein
    /// Agenten-Desktop teilen den Datenbestand und haben doch verschiedene Besucher.
    /// </remarks>
    /// <param name="context">Die aus dem Cookie gelesene Sitzung, oder <c>null</c>.</param>
    /// <param name="workspaceKey">Workspace der angefragten Fläche.</param>
    /// <param name="surfaceKey">Schlüssel der angefragten Fläche.</param>
    public static bool Matches(SurfaceCallerContext? context, string? workspaceKey, string? surfaceKey)
    {
        if (context is null ||
            string.IsNullOrWhiteSpace(workspaceKey) ||
            string.IsNullOrWhiteSpace(surfaceKey))
        {
            return false;
        }

        return string.Equals(context.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(context.SurfaceKey, surfaceKey, StringComparison.OrdinalIgnoreCase);
    }
}
