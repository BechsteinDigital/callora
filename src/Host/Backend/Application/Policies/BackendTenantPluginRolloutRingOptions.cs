namespace Callora.Host.Backend.Application.Policies;

/// <summary>
/// Tenant-specific rollout ring assignment for plugin activation.
/// </summary>
public sealed class BackendTenantPluginRolloutRingOptions
{
    /// <summary>
    /// Tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Rollout ring applied to this tenant.
    /// </summary>
    public PluginRolloutRing Ring { get; set; } = PluginRolloutRing.Stable;
}
