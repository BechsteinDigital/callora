namespace Callora.Administration.Api;

/// <summary>
/// The settings editor's view of one surface: the theme's fields, the values
/// this surface overrides, and the values it inherits from its workspace.
/// </summary>
/// <param name="InheritsWorkspaceValues">
/// False when the surface runs a different theme than its workspace — the
/// inherited map is then empty by definition.
/// </param>
public sealed record SurfaceThemeSettingsApiResponse(
    string WorkspaceKey,
    string SurfaceKey,
    bool HasAssignedTheme,
    string? ThemePluginId,
    string? ThemeVersion,
    bool InheritedFromWorkspace,
    bool InheritsWorkspaceValues,
    IReadOnlyList<WorkspaceThemeSettingDefinitionApiResponse> Fields,
    IReadOnlyDictionary<string, string> ValuesByKey,
    IReadOnlyDictionary<string, string> InheritedValuesByKey);
