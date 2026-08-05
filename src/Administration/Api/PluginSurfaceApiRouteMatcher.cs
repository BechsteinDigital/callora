using Callora.Core.Application.Surfaces;

namespace Callora.Administration.Api;

/// <summary>
/// Resolves a surface API request path against the mounted routes (#125 block B).
/// Only routes the inventory accepted are considered, so a refused declaration can
/// never be reached by guessing its path.
/// </summary>
public static class PluginSurfaceApiRouteMatcher
{
    /// <summary>Finds the route serving a request, or null.</summary>
    /// <param name="inventory">The mounted route inventory.</param>
    /// <param name="pluginId">Plugin id from the request path.</param>
    /// <param name="httpMethod">Request method.</param>
    /// <param name="routePath">Route path relative to the plugin root.</param>
    public static PluginSurfaceApiRouteMatch? FindMatch(
        SurfaceApiRouteInventory inventory,
        string pluginId,
        string httpMethod,
        string routePath)
    {
        ArgumentNullException.ThrowIfNull(inventory);

        var normalizedPluginId = RouteTemplateMatcher.NormalizeSegment(pluginId);
        var normalizedMethod = RouteTemplateMatcher.NormalizeSegment(httpMethod);
        var normalizedPath = RouteTemplateMatcher.NormalizePath(routePath);

        foreach (var mounted in inventory.Routes)
        {
            if (!string.Equals(mounted.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(
                    RouteTemplateMatcher.NormalizeSegment(mounted.Route.HttpMethod),
                    normalizedMethod,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (RouteTemplateMatcher.TryMatch(mounted.Route.RouteTemplate, normalizedPath, out var routeValues))
            {
                return new PluginSurfaceApiRouteMatch(mounted, routeValues);
            }
        }

        return null;
    }
}
