namespace Callora.Administration.Api;

/// <summary>
/// Public shape of one recorded entitlement decision. Scope is workspace &gt;
/// tenant &gt; platform depending on which keys are set (both null = platform-wide).
/// </summary>
public sealed record EntitlementApiResponse(
    string PluginId,
    string? WorkspaceKey,
    string? TenantKey,
    bool IsEntitled,
    string Source,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
