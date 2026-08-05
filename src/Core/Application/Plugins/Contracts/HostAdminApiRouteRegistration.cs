namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One plugin-provided Admin API route declaration.
/// </summary>
/// <param name="HttpMethod">HTTP method (for example: GET, POST, PUT, DELETE).</param>
/// <param name="RouteTemplate">Route template relative to plugin root (for example: sip-accounts/{accountId}).</param>
/// <param name="RequiredPermission">Permission key required for this route.</param>
/// <param name="Handler">Handler instance for this route.</param>
/// <param name="Scope">
/// Whether the route is workspace-scoped (the default) or explicitly global. A
/// workspace-scoped route only dispatches once the host resolved an effective
/// workspace and confirmed the plugin is available there (#109).
/// </param>
public sealed record HostAdminApiRouteRegistration(
    string HttpMethod,
    string RouteTemplate,
    string RequiredPermission,
    IHostAdminApiRouteHandler Handler,
    HostAdminApiRouteScope Scope = HostAdminApiRouteScope.Workspace);
