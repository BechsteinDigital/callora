namespace Callora.Core.Application.Extensions;

/// <summary>
/// Die wirksamen Theme-Werte einer öffentlich gerenderten Fläche.
/// </summary>
/// <remarks>
/// Als Port getrennt, weil die Auflösung teuer ist und deshalb einen Cache davor bekommt: Sie
/// fragt Workspace, Fläche, Definitionen, Werte auf zwei Ebenen und Sektionslayouts — sechs
/// Datenbankzugriffe für etwas, das sich zwischen zwei Verwaltungsvorgängen nicht ändert. Der
/// Renderpfad kennt nur diesen Vertrag und nicht, ob dahinter gerechnet oder erinnert wird.
/// </remarks>
public interface IWorkspacePublicThemeResolver
{
    /// <summary>Das Theme des Workspaces, ohne Übersteuerung durch eine Fläche.</summary>
    Task<WorkspacePublicTheme?> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Das wirksame Theme einer Fläche. Ein leerer oder unbekannter Flächenschlüssel fällt auf
    /// die Workspace-Ebene zurück.
    /// </summary>
    Task<WorkspacePublicTheme?> ResolveForSurfaceAsync(
        string workspaceKey,
        string? surfaceKey,
        CancellationToken cancellationToken = default);
}
