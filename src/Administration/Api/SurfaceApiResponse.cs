namespace Callora.Administration.Api;

public sealed record SurfaceApiResponse(
    Guid Id,
    string WorkspaceKey,
    string SurfaceKey,
    string DisplayName,
    string SurfaceType,
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix,
    string AccessMode,
    string Routing,
    string? Locale,
    string? TemplatePluginId,
    string? TemplateVersion,
    string? ThemePluginId,
    string? ThemeVersion,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>Der Elternknoten, oder null für eine Anwendungswurzel (ADR-019).</summary>
    public string? ParentSurfaceKey { get; init; }

    /// <summary>Reihenfolge unter Geschwistern.</summary>
    public int Position { get; init; }

    /// <summary>
    /// Die Claims, die DIESER Knoten verlangt — nicht die der Kette. Die Verwaltung muss zeigen,
    /// was hier gesetzt ist; was zusätzlich von oben gilt, gehört daneben und nicht ins
    /// Eingabefeld, sonst schriebe ein Speichern die Anforderung des Elternteils hierher fest.
    /// </summary>
    public string? RequiredClaims { get; init; }

    /// <summary>Claims, die jeder Besucher dieser Fläche mitbringt.</summary>
    public string? GrantedClaims { get; init; }
}
