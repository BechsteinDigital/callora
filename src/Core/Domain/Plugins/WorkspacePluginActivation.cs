namespace Callora.Core.Domain.Plugins;

/// <summary>
/// Stores per-workspace plugin runtime activation state.
/// </summary>
public sealed class WorkspacePluginActivation
{
    public Guid Id { get; set; }

    public string TenantKey { get; set; } = string.Empty;

    public string WorkspaceKey { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
