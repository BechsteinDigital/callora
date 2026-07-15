namespace Callora.Administration.Api;

public sealed record WorkspaceThemeSettingsApiResponse(
    string WorkspaceKey,
    bool HasAssignedTheme,
    string? ThemePluginId,
    string? ThemeVersion,
    IReadOnlyList<WorkspaceThemeSettingDefinitionApiResponse> Fields,
    IReadOnlyDictionary<string, string> ValuesByKey);
