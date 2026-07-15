namespace Callora.Administration.Api;

public sealed record WorkspaceThemeSettingDefinitionApiResponse(
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
