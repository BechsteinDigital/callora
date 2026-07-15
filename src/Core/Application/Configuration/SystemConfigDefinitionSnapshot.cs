namespace Callora.Core.Application.Configuration;

public sealed record SystemConfigDefinitionSnapshot(
    string PluginId,
    string Version,
    string ConfigKey,
    string Label,
    string FieldType,
    string? Description,
    string? DefaultValueJson,
    string? GroupName,
    string? OptionsJson,
    int SortOrder,
    bool IsActive);
