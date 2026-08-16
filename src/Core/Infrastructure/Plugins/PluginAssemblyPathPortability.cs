using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Infrastructure.Startup;

namespace Callora.Core.Infrastructure.Plugins;

/// <inheritdoc />
internal sealed class PluginAssemblyPathPortability(CalloraHostingOptions hostingOptions)
    : IPluginAssemblyPathPortability
{
    private const string PluginDirectoryToken = "${PluginDirectory}";
    private const string StaticPluginDirectoryToken = "${StaticPluginDirectory}";

    /// <inheritdoc />
    public string ToStoredPath(string fileSystemPath)
    {
        if (string.IsNullOrWhiteSpace(fileSystemPath) || !TryGetFullPath(fileSystemPath, out var full))
        {
            return fileSystemPath;
        }

        // Die längere Wurzel zuerst: Läge eine Wurzel unter der anderen, gewänne sonst die
        // allgemeinere, und derselbe Pfad bekäme je nach Reihenfolge ein anderes Token.
        foreach (var (token, root) in Roots().OrderByDescending(entry => entry.Root.Length))
        {
            if (!IsUnder(full, root))
            {
                continue;
            }

            var relative = Path.GetRelativePath(root, full);
            return $"{token}/{relative.Replace(Path.DirectorySeparatorChar, '/')}";
        }

        return fileSystemPath;
    }

    /// <inheritdoc />
    public string ToFileSystemPath(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return storedPath;
        }

        foreach (var (token, root) in Roots())
        {
            if (!storedPath.StartsWith(token + "/", StringComparison.Ordinal))
            {
                continue;
            }

            var relative = storedPath[(token.Length + 1)..].Replace('/', Path.DirectorySeparatorChar);
            // Ohne konfigurierte Wurzel bleibt nur der relative Rest. Das ergibt einen Pfad, der
            // nicht existiert — und das ist die ehrliche Antwort: Die Auflösung ist nicht
            // möglich, und der Aufrufer meldet die fehlende Assembly.
            return string.IsNullOrEmpty(root) ? relative : Path.GetFullPath(Path.Combine(root, relative));
        }

        return storedPath;
    }

    /// <inheritdoc />
    public bool IsUnderPluginRoots(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return false;
        }

        if (storedPath.StartsWith(PluginDirectoryToken + "/", StringComparison.Ordinal)
            || storedPath.StartsWith(StaticPluginDirectoryToken + "/", StringComparison.Ordinal))
        {
            return true;
        }

        return TryGetFullPath(storedPath, out var full)
               && Roots().Any(entry => IsUnder(full, entry.Root));
    }

    private IEnumerable<(string Token, string Root)> Roots()
    {
        yield return (PluginDirectoryToken, ResolveRoot(hostingOptions.PluginDirectory));
        yield return (StaticPluginDirectoryToken, ResolveRoot(hostingOptions.StaticPluginDirectory));
    }

    private static string ResolveRoot(string? configured)
        => string.IsNullOrWhiteSpace(configured)
            ? string.Empty
            : CalloraHostingPathResolver.ResolvePluginDirectory(configured);

    private static bool IsUnder(string fullPath, string root)
    {
        if (string.IsNullOrEmpty(root) || !TryGetFullPath(root, out var fullRoot))
        {
            return false;
        }

        return fullPath.StartsWith(
            fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullPath = string.Empty;
            return false;
        }
    }
}
