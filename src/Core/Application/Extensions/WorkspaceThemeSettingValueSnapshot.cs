namespace Callora.Core.Application.Extensions;

/// <summary>
/// One stored theme setting value. <paramref name="SurfaceKey"/> is empty for a
/// workspace-level value and carries the surface key for a surface override.
/// </summary>
public sealed record WorkspaceThemeSettingValueSnapshot(
    string WorkspaceKey,
    string SurfaceKey,
    string PluginId,
    string SettingKey,
    string ValueJson,
    DateTimeOffset UpdatedAtUtc);
