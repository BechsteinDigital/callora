namespace Callora.Host.Workspace.Api;

public sealed record WorkspaceThemeSettingsApiResponse(
    string WorkspaceKey,
    bool HasAssignedTheme,
    string? ThemePluginId,
    string? ThemeVersion,
    IReadOnlyList<WorkspaceThemeSettingDefinitionApiResponse> Fields,
    IReadOnlyDictionary<string, string> ValuesByKey);
