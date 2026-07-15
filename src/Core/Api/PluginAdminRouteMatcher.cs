using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Api;

public static class PluginAdminRouteMatcher
{
    public static PluginAdminRouteMatch? FindMatch(
        IEnumerable<IHostAdminApiExtensionContributor> contributors,
        string pluginId,
        string httpMethod,
        string routePath)
    {
        var normalizedPluginId = NormalizeSegment(pluginId);
        var normalizedMethod = NormalizeSegment(httpMethod);
        var normalizedPath = NormalizePath(routePath);

        foreach (var contributor in contributors)
        {
            if (!string.Equals(contributor.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var route in contributor.Routes)
            {
                if (!string.Equals(NormalizeSegment(route.HttpMethod), normalizedMethod, StringComparison.Ordinal))
                    continue;

                if (!TryMatchTemplate(route.RouteTemplate, normalizedPath, out var routeValues))
                    continue;

                return new PluginAdminRouteMatch(contributor, route, routeValues);
            }
        }

        return null;
    }

    private static string NormalizeSegment(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return string.Join(
            '/',
            value
                .Trim()
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static bool TryMatchTemplate(
        string routeTemplate,
        string routePath,
        out IReadOnlyDictionary<string, string> routeValues)
    {
        var normalizedTemplate = NormalizePath(routeTemplate);

        var templateSegments = normalizedTemplate.Length == 0
            ? Array.Empty<string>()
            : normalizedTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = routePath.Length == 0
            ? Array.Empty<string>()
            : routePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (templateSegments.Length != pathSegments.Length)
        {
            routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < templateSegments.Length; i++)
        {
            var templateSegment = templateSegments[i];
            var pathSegment = pathSegments[i];

            if (TryReadRouteValueName(templateSegment, out var routeValueName))
            {
                values[routeValueName] = pathSegment;
                continue;
            }

            if (!string.Equals(templateSegment, pathSegment, StringComparison.OrdinalIgnoreCase))
            {
                routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                return false;
            }
        }

        routeValues = values;
        return true;
    }

    private static bool TryReadRouteValueName(string templateSegment, out string routeValueName)
    {
        routeValueName = string.Empty;
        if (!templateSegment.StartsWith("{", StringComparison.Ordinal) ||
            !templateSegment.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        var candidate = templateSegment[1..^1].Trim();
        if (candidate.Length == 0)
            return false;

        routeValueName = candidate;
        return true;
    }
}
