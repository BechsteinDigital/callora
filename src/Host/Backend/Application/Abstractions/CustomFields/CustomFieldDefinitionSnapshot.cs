namespace Callora.Host.Backend.Application.Abstractions.CustomFields;

public sealed record CustomFieldDefinitionSnapshot(
    string PluginId,
    string Version,
    string EntityName,
    string FieldKey,
    string Label,
    string FieldType,
    int SortOrder,
    bool IsActive);
