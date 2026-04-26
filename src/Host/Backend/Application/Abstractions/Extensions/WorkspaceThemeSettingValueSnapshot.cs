namespace Callora.Host.Backend.Application.Abstractions.Extensions;

public sealed record WorkspaceThemeSettingValueSnapshot(
    string WorkspaceKey,
    string PluginId,
    string SettingKey,
    string ValueJson,
    DateTimeOffset UpdatedAtUtc);
