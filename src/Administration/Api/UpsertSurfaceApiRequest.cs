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
    bool IsActive);
