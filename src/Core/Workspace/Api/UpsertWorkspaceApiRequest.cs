namespace Callora.Host.Workspace.Api;

public sealed record UpsertWorkspaceApiRequest(
    string? TenantKey,
    string DisplayName,
    string WorkspaceType,
    bool IsActive,
    string? PublicBaseUrl = null);
