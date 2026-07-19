using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

/// <summary>Editable fields of a workspace surface for upsert.</summary>
public sealed record WorkspaceSurfaceInput(
    string SurfaceKey,
    string DisplayName,
    string SurfaceType,
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix,
    SurfaceAccessMode AccessMode,
    string? Locale,
    string? TemplatePluginId,
    string? TemplateVersion,
    string? ThemePluginId,
    string? ThemeVersion,
    bool IsActive);
