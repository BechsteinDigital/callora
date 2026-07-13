namespace Callora.Host.Backend.Domain.Entitlements;

/// <summary>
/// One entitlement decision: whether a plugin may be used in a scope.
/// Deliberately separate from workspace activation — "allowed to use" and
/// "switched on" are distinct domain states (PLAT-253).
/// </summary>
public sealed class PluginEntitlement
{
    public Guid Id { get; set; }

    public string PluginId { get; set; } = string.Empty;

    /// <summary>Tenant scope; null for platform-wide entitlements.</summary>
    public string? TenantKey { get; set; }

    /// <summary>Workspace scope; null for tenant- or platform-wide entitlements.</summary>
    public string? WorkspaceKey { get; set; }

    public bool IsEntitled { get; set; }

    /// <summary>Origin of the decision, e.g. "marketplace", "manual", "migrated".</summary>
    public string Source { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
