using Callora.Core.Application.Surfaces;

namespace Callora.Administration.Api;

/// <summary>
/// A surface API request matched against a mounted plugin route (#125 block B).
/// </summary>
/// <param name="Mounted">The mounted route and its owning plugin.</param>
/// <param name="RouteValues">Values extracted from the route template.</param>
public sealed record PluginSurfaceApiRouteMatch(
    SurfaceApiMountedRoute Mounted,
    IReadOnlyDictionary<string, string> RouteValues);
