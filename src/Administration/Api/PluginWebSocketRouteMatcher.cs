using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Administration.Api;

/// <summary>
/// Resolves an incoming <c>/ws/{pluginId}/{routePath}</c> request to a declared
/// plugin WebSocket route. Unlike the Admin-API matcher there is no HTTP method to
/// discriminate on — a WebSocket upgrade is always a GET — so matching is purely by
/// plugin id and route template.
/// </summary>
public static class PluginWebSocketRouteMatcher
{
    public static PluginWebSocketRouteMatch? FindMatch(
        IEnumerable<IHostWebSocketEndpointContributor> contributors,
        string pluginId,
        string routePath)
    {
        var normalizedPluginId = RouteTemplateMatcher.NormalizeSegment(pluginId);
        var normalizedPath = RouteTemplateMatcher.NormalizePath(routePath);

        foreach (var contributor in contributors)
        {
            if (!string.Equals(contributor.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var route in contributor.WebSocketRoutes)
            {
                if (RouteTemplateMatcher.TryMatch(route.RouteTemplate, normalizedPath, out var routeValues))
                {
                    return new PluginWebSocketRouteMatch(contributor, route, routeValues);
                }
            }
        }

        return null;
    }
}
