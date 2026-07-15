namespace Callora.Core.Application.Configuration;

public sealed record SystemConfigDefinitionInput(
    string ConfigKey,
    string Label,
    string FieldType,
    string? Description,
    string? DefaultValueJson,
    string? GroupName,
    string? OptionsJson,
    int SortOrder,
    bool IsActive);
