using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// The loaded workspace and surface for one theme operation, plus the derived
/// answers every caller needs: which theme applies, and whether the workspace
/// values carry over.
/// </summary>
internal sealed record SurfaceThemeContext(
    SurfaceThemeStatus Status,
    WorkspaceSnapshot? Workspace = null,
    WorkspaceSurfaceSnapshot? Surface = null,
    string? Message = null)
{
    /// <summary>The surface's own theme, or the workspace's when it has none.</summary>
    public string? EffectiveThemePluginId =>
        Coalesce(Surface?.ThemePluginId, Workspace?.ThemePluginId);

    public string? EffectiveThemeVersion =>
        Coalesce(Surface?.ThemeVersion, Workspace?.ThemeVersion);

    /// <summary>True while the surface simply follows its workspace.</summary>
    public bool InheritedFromWorkspace => string.IsNullOrWhiteSpace(Surface?.ThemePluginId);

    /// <summary>
    /// Workspace values apply only while both levels run the same theme —
    /// otherwise they describe another theme's setting keys.
    /// </summary>
    public bool InheritsWorkspaceValues =>
        EffectiveThemePluginId is not null &&
        !string.IsNullOrWhiteSpace(Workspace?.ThemePluginId) &&
        string.Equals(Workspace!.ThemePluginId, EffectiveThemePluginId, StringComparison.OrdinalIgnoreCase);

    private static string? Coalesce(string? preferred, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            return preferred;
        }

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback;
    }
}
