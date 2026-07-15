namespace Callora.Administration.Api;

public sealed record UpsertWorkspaceApiRequest(
    string? TenantKey,
    string DisplayName,
    string WorkspaceType,
    bool IsActive,
    string? PublicBaseUrl = null);
