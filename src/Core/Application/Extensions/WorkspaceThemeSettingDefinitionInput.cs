namespace Callora.Core.Application.Extensions;

public sealed record WorkspaceThemeSettingDefinitionInput(
    string SettingKey,
    string Label,
    string FieldType,
    string? Description,
    string? DefaultValueJson,
    bool IsRequired,
    int SortOrder,
    string? GroupName,
    string? OptionsJson,
    bool IsActive);
