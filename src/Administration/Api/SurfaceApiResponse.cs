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
}
