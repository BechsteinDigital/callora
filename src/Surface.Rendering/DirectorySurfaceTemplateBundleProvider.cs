namespace Callora.Surface.Rendering;

/// <summary>
/// A simple <see cref="ISurfaceTemplateBundleProvider"/> backed by a fixed map of
/// bundle id → root directory. The distribution wires this from the installed
/// template plugins (bundle id = plugin id, root = the plugin's views root for the
/// surface type). The loader still confines every resolved path to its root.
/// </summary>
public sealed class DirectorySurfaceTemplateBundleProvider : ISurfaceTemplateBundleProvider
{
    private readonly Dictionary<string, string> _roots;

    public DirectorySurfaceTemplateBundleProvider(IReadOnlyDictionary<string, string> bundleRoots)
    {
        ArgumentNullException.ThrowIfNull(bundleRoots);
        _roots = new Dictionary<string, string>(bundleRoots, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetBundleRoot(string bundleId, out string? rootFullPath)
    {
        if (!string.IsNullOrWhiteSpace(bundleId) && _roots.TryGetValue(bundleId, out var root))
        {
            rootFullPath = root;
            return true;
        }

        rootFullPath = null;
        return false;
    }
}
