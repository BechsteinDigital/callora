namespace Callora.Core.Application.Entitlements;

/// <summary>
/// Read-only projection of one entitlement decision (a row of
/// <c>plugin_entitlements</c>) for operator listing. Mirrors the domain entity
/// without exposing its mutable surface; the scope is workspace &gt; tenant &gt;
/// platform depending on which keys are set (both null = platform-wide).
/// </summary>
public sealed record PluginEntitlementSnapshot(
    string PluginId,
    string? WorkspaceKey,
    string? TenantKey,
    bool IsEntitled,
    string Source,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
