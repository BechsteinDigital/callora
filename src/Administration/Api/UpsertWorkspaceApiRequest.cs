namespace Callora.Administration.Api;

/// <param name="DefaultSurfaceBaseUrl">
/// Convenience for the common one-surface case: configures the route of the
/// workspace's "default" surface. Further routes are managed per surface under
/// <c>/api/workspaces/{workspaceKey}/surfaces</c>.
/// </param>
/// <param name="PublicHost">
/// Der Host, unter dem dieser Workspace erreichbar ist — <c>kunde.de</c>. Leer lassen, wenn
/// er über einen Pfad erreicht wird: dann beginnt jede Oberflächen-URL mit dem
/// Workspace-Schlüssel. Ein Host auf einer OBERFLÄCHE ist das speziellere Signal und
/// gewinnt gegen diesen hier.
/// </param>
public sealed record UpsertWorkspaceApiRequest(
    string? TenantKey,
    string DisplayName,
    string WorkspaceType,
    bool IsActive,
    string? DefaultSurfaceBaseUrl = null,
    string? PublicHost = null);
