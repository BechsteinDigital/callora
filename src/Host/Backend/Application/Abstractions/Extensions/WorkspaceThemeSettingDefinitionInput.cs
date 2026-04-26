namespace Callora.Host.Backend.Application.Abstractions.Extensions;

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
