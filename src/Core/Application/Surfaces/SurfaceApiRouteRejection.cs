namespace Callora.Core.Application.Surfaces;

/// <summary>
/// A declared surface API route the host refused to mount (#125 block B). Kept rather
/// than discarded: a route silently missing is the hardest kind of misconfiguration to
/// diagnose from the outside.
/// </summary>
/// <param name="PluginId">Plugin that declared the route.</param>
/// <param name="HttpMethod">Declared HTTP method.</param>
/// <param name="RouteTemplate">Declared route template.</param>
/// <param name="Reason">Why it was refused.</param>
public sealed record SurfaceApiRouteRejection(
    string PluginId,
    string HttpMethod,
    string RouteTemplate,
    SurfaceApiRouteRejectionReason Reason);
