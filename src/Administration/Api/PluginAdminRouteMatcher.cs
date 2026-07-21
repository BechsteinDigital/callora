using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Administration.Api;

public static class PluginAdminRouteMatcher
{
    public static PluginAdminRouteMatch? FindMatch(
        IEnumerable<IHostAdminApiExtensionContributor> contributors,
        string pluginId,
        string httpMethod,
        string routePath)
    {
        var normalizedPluginId = RouteTemplateMatcher.NormalizeSegment(pluginId);
        var normalizedMethod = RouteTemplateMatcher.NormalizeSegment(httpMethod);
        var normalizedPath = RouteTemplateMatcher.NormalizePath(routePath);

        foreach (var contributor in contributors)
        {
            if (!string.Equals(contributor.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var route in contributor.Routes)
            {
                if (!string.Equals(RouteTemplateMatcher.NormalizeSegment(route.HttpMethod), normalizedMethod, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!RouteTemplateMatcher.TryMatch(route.RouteTemplate, normalizedPath, out var routeValues))
                {
                    continue;
                }

                return new PluginAdminRouteMatch(contributor, route, routeValues);
            }
        }

        return null;
    }
}
