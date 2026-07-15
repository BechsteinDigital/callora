namespace Callora.Administration.Api;

public sealed record PluginWorkspaceEntitlementApiResponse(
    string WorkspaceKey,
    string PluginId,
    bool IsEntitled,
    string? TenantKey = null);
