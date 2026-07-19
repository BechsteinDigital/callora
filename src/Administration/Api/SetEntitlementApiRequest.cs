namespace Callora.Administration.Api;

/// <summary>
/// Body for granting or revoking a plugin entitlement for a scope. Both keys null
/// = platform-wide; tenant only = whole tenant; workspace set = that workspace.
/// </summary>
public sealed record SetEntitlementApiRequest(
    string PluginId,
    string? WorkspaceKey,
    string? TenantKey,
    bool IsEntitled);
