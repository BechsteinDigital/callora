namespace Callora.Host.Backend.Domain.Entitlements;

/// <summary>
/// One processed marketplace entitlement event; ensures idempotent replay.
/// </summary>
public sealed class MarketplaceEntitlementEventRecord
{
    private MarketplaceEntitlementEventRecord()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>External event identifier used as idempotency key.</summary>
    public string EventId { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string PluginId { get; private set; } = string.Empty;

    public string TenantKey { get; private set; } = string.Empty;

    public string? WorkspaceKey { get; private set; }

    public DateTimeOffset ProcessedAtUtc { get; private set; }

    public static MarketplaceEntitlementEventRecord Create(
        string eventId,
        string action,
        string pluginId,
        string tenantKey,
        string? workspaceKey,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        return new MarketplaceEntitlementEventRecord
        {
            Id = Guid.NewGuid(),
            EventId = eventId.Trim(),
            Action = action.Trim(),
            PluginId = pluginId.Trim(),
            TenantKey = tenantKey.Trim(),
            WorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? null : workspaceKey.Trim(),
            ProcessedAtUtc = nowUtc
        };
    }
}
