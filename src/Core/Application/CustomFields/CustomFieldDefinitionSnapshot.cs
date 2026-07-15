namespace Callora.Core.Application.CustomFields;

public sealed record CustomFieldDefinitionSnapshot(
    string PluginId,
    string Version,
    string EntityName,
    string FieldKey,
    string Label,
    string FieldType,
    int SortOrder,
    bool IsActive);
