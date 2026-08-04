namespace Callora.Administration.Api;

/// <summary>Assigns a theme to one surface, overriding its workspace.</summary>
public sealed record SurfaceThemeAssignmentUpsertApiRequest(
    string ThemePluginId,
    string ThemeVersion);
