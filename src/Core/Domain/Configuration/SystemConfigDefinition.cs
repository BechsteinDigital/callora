namespace Callora.Core.Domain.Configuration;

/// <summary>
/// One configuration field declared by a plugin (or the host) via the
/// registry.json config schema. Values are stored separately per scope.
/// </summary>
public sealed class SystemConfigDefinition
{
    public Guid Id { get; set; }

    /// <summary>Owning plugin; "host" for platform-core settings.</summary>
    public string PluginId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    /// <summary>Full key, conventionally "&lt;area&gt;.&lt;name&gt;", e.g. "smtp.host".</summary>
    public string ConfigKey { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Field type: text, number, bool, select, color, secret.</summary>
    public string FieldType { get; set; } = "text";

    public string? Description { get; set; }

    public string? DefaultValueJson { get; set; }

    public string? GroupName { get; set; }

    public string? OptionsJson { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
