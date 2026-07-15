namespace Callora.Core.Application.Extensions;

public sealed record WorkspaceThemeSettingDefinitionSnapshot(
    string SettingKey,
    string PluginId,
    string Version,
    string Label,
    string FieldType,
    string? Description,
    string? DefaultValueJson,
    bool IsRequired,
    int SortOrder,
    string? GroupName,
    string? OptionsJson,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
