namespace Callora.Core.Domain.CustomFields;

/// <summary>
/// One custom field a plugin attaches to an entity — a core entity such as
/// workspace or user, or an entity the plugin itself defines.
/// </summary>
public sealed class CustomFieldDefinition
{
    public Guid Id { get; set; }

    public string PluginId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    /// <summary>Target entity name: a core entity (e.g. workspace, user) or a plugin-defined entity.</summary>
    public string EntityName { get; set; } = string.Empty;

    public string FieldKey { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string FieldType { get; set; } = "text";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
