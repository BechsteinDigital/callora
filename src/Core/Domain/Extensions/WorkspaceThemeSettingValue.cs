namespace Callora.Core.Domain.Extensions;

public sealed class WorkspaceThemeSettingValue
{
    public Guid Id { get; set; }

    public string WorkspaceKey { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public string SettingKey { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "null";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
