namespace Callora.Core.Application.Extensions;

/// <summary>
/// The settings editor's view of one surface: what the theme offers, what this
/// surface overrides, and what it would otherwise inherit.
/// </summary>
/// <param name="InheritsWorkspaceValues">
/// False when the surface runs a different theme than its workspace — the
/// workspace values then belong to another theme and do not apply.
/// </param>
/// <param name="OwnValuesByKey">Values stored on the surface itself.</param>
/// <param name="InheritedValuesByKey">
/// Values that apply where the surface has none of its own. Empty when nothing
/// is inherited.
/// </param>
public sealed record SurfaceThemeSettings(
    string WorkspaceKey,
    string SurfaceKey,
    bool HasAssignedTheme,
    string? ThemePluginId,
    string? ThemeVersion,
    bool InheritedFromWorkspace,
    bool InheritsWorkspaceValues,
    IReadOnlyList<WorkspaceThemeSettingDefinitionSnapshot> Fields,
    IReadOnlyDictionary<string, string> OwnValuesByKey,
    IReadOnlyDictionary<string, string> InheritedValuesByKey);
