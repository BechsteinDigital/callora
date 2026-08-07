namespace Callora.Administration.Api;

/// <summary>Body for creating/updating a surface. The surface key comes from the route.</summary>
public sealed record UpsertSurfaceApiRequest(
    string DisplayName,
    string SurfaceType,
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix,
    string AccessMode,
    string? Locale,
    string? TemplatePluginId,
    string? TemplateVersion,
    string? ThemePluginId,
    string? ThemeVersion,
    bool IsActive)
{
    /// <summary>
    /// Der Elternknoten innerhalb desselben Workspaces, oder null für eine Anwendungswurzel
    /// (ADR-019). Weggelassen heißt Wurzel — was jeder bestehende Aufruf meint.
    /// </summary>
    public string? ParentSurfaceKey { get; init; }

    /// <summary>Reihenfolge unter Geschwistern; die Reihenfolge der Navigation.</summary>
    public int Position { get; init; }

    /// <summary>
    /// Claims, die ein Besucher mitbringen muss (ADR-019 §4) — kommagetrennt, leer heißt keine
    /// Anforderung. Kumulativ entlang der Kette: Was ein Elternteil verlangt, gilt auch hier.
    /// </summary>
    public string? RequiredClaims { get; init; }
}
