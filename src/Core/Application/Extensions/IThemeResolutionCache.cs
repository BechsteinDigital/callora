namespace Callora.Core.Application.Extensions;

/// <summary>
/// Der Auslöser, der das gecachte Theme verwirft.
/// </summary>
/// <remarks>
/// <para>
/// Getrennt von <see cref="IWorkspacePublicThemeResolver"/>, weil beide verschiedene Seiten
/// bedienen: Der Renderpfad liest und soll nichts verwerfen können, die Persistenz schreibt und
/// braucht nichts zu lesen. Dasselbe Muster wie bei
/// <c>IWorkspaceTemplateResolutionCache</c>.
/// </para>
/// <para>
/// Zu rufen von jedem Schreibvorgang, der Theme-Zuordnung, Definitionen, Werte oder
/// Sektionslayouts berührt. Ein vergessener Aufruf zeigt sich nicht als Fehler, sondern als
/// Betreiber, der eine Farbe ändert und sie nicht wiederfindet — bis die Rückfallzeit abläuft und
/// der Zusammenhang endgültig verwischt ist.
/// </para>
/// </remarks>
public interface IThemeResolutionCache
{
    /// <summary>Verwirft alle gecachten Theme-Auflösungen.</summary>
    void Invalidate();
}
