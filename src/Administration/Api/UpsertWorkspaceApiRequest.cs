namespace Callora.Administration.Api;

/// <param name="DefaultSurfaceBaseUrl">
/// Convenience for the common one-surface case: configures the route of the
/// workspace's "default" surface. The workspace itself has no address — further
/// routes are managed per surface under
/// <c>/api/workspaces/{workspaceKey}/surfaces</c>.
/// </param>
public sealed record UpsertWorkspaceApiRequest(
    string? TenantKey,
    string DisplayName,
    string WorkspaceType,
    bool IsActive,
    string? DefaultSurfaceBaseUrl = null);
