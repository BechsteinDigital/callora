namespace Callora.Core.Application.Surfaces;

/// <summary>
/// What a plugin may mount under the surface API prefix (#125 block B). The plugin
/// id in the path already separates plugins from each other, so what is left to guard
/// is a route escaping its own root or shadowing a platform path.
/// </summary>
public static class SurfaceApiRouteRules
{
    /// <summary>The reserved prefix every plugin surface route is mounted under.</summary>
    public const string Prefix = "/surface-api";

    // Paths the platform owns on a surface host. A plugin id colliding with one of
    // these would put plugin routes where the host's own belong, so the contributor
    // is not mounted at all.
    private static readonly string[] ReservedPluginIds =
    [
        "api", "admin", "login", "logout", "public", "ws", "surface", "surface-api", "surface-app", "health",
    ];

    /// <summary>
    /// Whether a plugin may claim this id on the surface API prefix.
    /// </summary>
    /// <param name="pluginId">Plugin id to test.</param>
    public static bool IsAllowedPluginId(string? pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        var normalized = pluginId.Trim();
        return !normalized.Contains('/', StringComparison.Ordinal) &&
               !normalized.Contains('\\', StringComparison.Ordinal) &&
               !ReservedPluginIds.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Whether a route template may be mounted. Absolute paths and traversal segments
    /// are refused: a template is a name inside the plugin's own root, not a way out
    /// of it.
    /// </summary>
    /// <param name="routeTemplate">Template to test.</param>
    public static bool IsAllowedTemplate(string? routeTemplate)
    {
        if (routeTemplate is null)
        {
            return false;
        }

        var normalized = routeTemplate.Trim();
        if (normalized.StartsWith('/') || normalized.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is ".." or ".")
            {
                return false;
            }
        }

        return true;
    }
}
