namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// One plugin-provided Admin API route declaration.
/// </summary>
/// <param name="HttpMethod">HTTP method (for example: GET, POST, PUT, DELETE).</param>
/// <param name="RouteTemplate">Route template relative to plugin root (for example: sip-accounts/{accountId}).</param>
/// <param name="RequiredPermission">Permission key required for this route.</param>
/// <param name="Handler">Handler instance for this route.</param>
public sealed record HostAdminApiRouteRegistration(
    string HttpMethod,
    string RouteTemplate,
    string RequiredPermission,
    IHostAdminApiRouteHandler Handler);
