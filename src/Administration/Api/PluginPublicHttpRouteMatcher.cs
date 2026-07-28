using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Administration.Api;

/// <summary>
/// Resolves an incoming <c>/public/{pluginId}/{routePath}</c> request to a declared
/// plugin public HTTP route. Matching discriminates on both the HTTP method and the
/// route template, since public endpoints support GET and POST (unlike WebSocket
/// routes, which are always a GET upgrade).
/// </summary>
public static class PluginPublicHttpRouteMatcher
{
    /// <summary>
    /// Searches <paramref name="contributors"/> for a route whose plugin identifier,
    /// HTTP method, and route template all match the incoming request.
    /// </summary>
    /// <param name="contributors">All registered public HTTP endpoint contributors.</param>
    /// <param name="pluginId">The <c>{pluginId}</c> segment from the URL.</param>
    /// <param name="httpMethod">The incoming HTTP method (for example: <c>GET</c>, <c>POST</c>).</param>
    /// <param name="routePath">The <c>{**routePath}</c> catch-all from the URL.</param>
    /// <returns>
    /// A <see cref="PluginPublicHttpRouteMatch"/> when exactly one route matches,
    /// or <c>null</c> when no contributor owns the given plugin/method/path combination.
    /// </returns>
    public static PluginPublicHttpRouteMatch? FindMatch(
        IEnumerable<IHostPublicHttpEndpointContributor> contributors,
        string pluginId,
        string httpMethod,
        string routePath)
    {
        var normalizedPluginId = RouteTemplateMatcher.NormalizeSegment(pluginId);
        var normalizedMethod = RouteTemplateMatcher.NormalizeSegment(httpMethod).ToUpperInvariant();
        var normalizedPath = RouteTemplateMatcher.NormalizePath(routePath);

        foreach (var contributor in contributors)
        {
            if (!string.Equals(contributor.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var route in contributor.PublicHttpRoutes)
            {
                if (!string.Equals(
                        RouteTemplateMatcher.NormalizeSegment(route.Method).ToUpperInvariant(),
                        normalizedMethod,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!RouteTemplateMatcher.TryMatch(route.RouteTemplate, normalizedPath, out var routeValues))
                {
                    continue;
                }

                return new PluginPublicHttpRouteMatch(contributor, route, routeValues);
            }
        }

        return null;
    }
}
