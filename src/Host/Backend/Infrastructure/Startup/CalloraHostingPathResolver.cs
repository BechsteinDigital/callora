namespace Callora.Host.Backend.Infrastructure.Startup;

/// <summary>
/// Resolves hosting paths so local and containerized development use the same plugin location.
/// </summary>
internal static class CalloraHostingPathResolver
{
    /// <summary>
    /// Resolves the configured plugin directory to an absolute path.
    /// </summary>
    /// <param name="configuredPath">Configured plugin directory path.</param>
    /// <returns>An absolute plugin directory path when configured; otherwise an empty string.</returns>
    public static string ResolvePluginDirectory(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return string.Empty;
        }

        var primary = Path.GetFullPath(configuredPath);
        if (Path.IsPathRooted(configuredPath) || Directory.Exists(primary))
        {
            return primary;
        }

        var current = Directory.GetCurrentDirectory();
        for (var depth = 0; depth < 6; depth++)
        {
            var candidate = Path.GetFullPath(Path.Combine(current, configuredPath));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return primary;
    }
}
