namespace Callora.Administration.Api;

/// <summary>
/// Shared segment-based matcher for plugin route templates (<c>segment/{param}</c>).
/// Both the Admin-API and WebSocket route matchers resolve a request path against a
/// contributor's declared templates the same way, so the parsing lives here once.
/// </summary>
internal static class RouteTemplateMatcher
{
    /// <summary>
    /// Attempts to match <paramref name="routePath"/> against <paramref name="routeTemplate"/>,
    /// extracting any <c>{param}</c> segments into <paramref name="routeValues"/> (case-insensitive keys).
    /// </summary>
    public static bool TryMatch(
        string routeTemplate,
        string routePath,
        out IReadOnlyDictionary<string, string> routeValues)
    {
        var templateSegments = SplitPath(routeTemplate);
        var pathSegments = SplitPath(routePath);

        if (templateSegments.Length != pathSegments.Length)
        {
            routeValues = EmptyValues();
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
                routeValues = EmptyValues();
                return false;
            }
        }

        routeValues = values;
        return true;
    }

    /// <summary>Trims, de-slashes and drops empty segments so paths compare canonically.</summary>
    public static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(
            '/',
            value
                .Trim()
                .Trim('/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>Trims a single path/method segment, mapping blank to empty.</summary>
    public static string NormalizeSegment(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string[] SplitPath(string value)
    {
        var normalized = NormalizePath(value);
        return normalized.Length == 0
            ? Array.Empty<string>()
            : normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool TryReadRouteValueName(string templateSegment, out string routeValueName)
    {
        routeValueName = string.Empty;
        if (!templateSegment.StartsWith('{') || !templateSegment.EndsWith('}'))
        {
            return false;
        }

        var candidate = templateSegment[1..^1].Trim();
        if (candidate.Length == 0)
        {
            return false;
        }

        routeValueName = candidate;
        return true;
    }

    private static Dictionary<string, string> EmptyValues() => new(StringComparer.OrdinalIgnoreCase);
}
