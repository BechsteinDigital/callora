namespace Callora.Host.Backend.Domain.Extensions;

public sealed class WorkspaceThemeSettingDefinition
{
    public Guid Id { get; set; }

    public string SettingKey { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string FieldType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? DefaultValueJson { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public string? GroupName { get; set; }

    public string? OptionsJson { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
