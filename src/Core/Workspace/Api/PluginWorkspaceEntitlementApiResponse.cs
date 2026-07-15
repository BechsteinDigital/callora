namespace Callora.Host.Workspace.Api;

public sealed record PluginWorkspaceEntitlementApiResponse(
    string WorkspaceKey,
    string PluginId,
    bool IsEntitled,
    string? TenantKey = null);
