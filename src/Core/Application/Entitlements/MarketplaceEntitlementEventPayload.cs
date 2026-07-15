namespace Callora.Core.Application.Entitlements;

/// <summary>
/// Inbound marketplace entitlement event (grant/revoke). The host contains
/// no billing logic; the marketplace translates commerce into these events.
/// </summary>
public sealed record MarketplaceEntitlementEventPayload(
    string EventId,
    string Action,
    string PluginId,
    string TenantKey,
    string? WorkspaceKey);
