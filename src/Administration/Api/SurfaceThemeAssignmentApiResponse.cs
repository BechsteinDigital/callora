namespace Callora.Administration.Api;

/// <summary>
/// The theme a surface renders with. <paramref name="InheritedFromWorkspace"/>
/// tells the caller whether that is the surface's own choice or the workspace's.
/// </summary>
public sealed record SurfaceThemeAssignmentApiResponse(
    string WorkspaceKey,
    string SurfaceKey,
    string? ThemePluginId,
    string? ThemeVersion,
    bool InheritedFromWorkspace);
