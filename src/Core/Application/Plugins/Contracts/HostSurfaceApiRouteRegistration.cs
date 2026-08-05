namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One plugin-provided surface API route declaration (#125 block B).
/// </summary>
/// <param name="HttpMethod">HTTP method (for example: GET, POST, PUT, DELETE).</param>
/// <param name="RouteTemplate">
/// Route template relative to the plugin's surface API root (for example:
/// <c>rooms/{roomId}/invitations</c>). Must be relative and free of traversal
/// segments; anything else is rejected rather than mounted.
/// </param>
/// <param name="Handler">Handler instance for this route.</param>
/// <param name="Audience">
/// Whether the route needs an authenticated caller (the default) or accepts a guest
/// context as well.
/// </param>
public sealed record HostSurfaceApiRouteRegistration(
    string HttpMethod,
    string RouteTemplate,
    IHostSurfaceApiRouteHandler Handler,
    SurfaceApiRouteAudience Audience = SurfaceApiRouteAudience.Authenticated);
