namespace Callora.Core.Domain.Extensions;

public sealed class WorkspaceThemeSettingValue
{
    public Guid Id { get; set; }

    public string WorkspaceKey { get; set; } = string.Empty;

    /// <summary>
    /// The surface this value belongs to, or empty for the workspace level.
    /// <para>
    /// A surface overrides the workspace the same way a workspace overrides the
    /// platform in the configuration scopes. Empty rather than null: the unique
    /// index spans this column, and most databases treat NULLs as distinct —
    /// which would silently permit duplicate workspace-level rows.
    /// </para>
    /// </summary>
    public string SurfaceKey { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public string SettingKey { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "null";

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
