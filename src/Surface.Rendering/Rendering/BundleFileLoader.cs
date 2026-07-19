namespace Callora.Surface.Rendering.Rendering;

/// <summary>
/// Resolves and reads a <c>@bundle/path</c> template file for the JS engine's
/// Nunjucks loader (ADR-015 §8). Confined exactly like the former Scriban loader:
/// only bundles in scope resolve, and every path is canonicalised and verified to
/// stay UNDER its bundle root — <c>../</c> and absolute paths are rejected.
/// Returns null when a name is malformed / out of scope / escapes / missing, so
/// the JS loader turns it into a template error.
/// </summary>
internal sealed class BundleFileLoader
{
    private readonly ISurfaceTemplateBundleProvider _provider;
    private readonly HashSet<string> _bundlesInScope;

    public BundleFileLoader(ISurfaceTemplateBundleProvider provider, IEnumerable<string> bundlesInScope)
    {
        _provider = provider;
        _bundlesInScope = new HashSet<string>(bundlesInScope, StringComparer.OrdinalIgnoreCase);
    }

    public string? TryLoad(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName) || templateName[0] != '@')
        {
            return null;
        }

        var slash = templateName.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 1 || slash == templateName.Length - 1)
        {
            return null;
        }

        var bundleId = templateName[1..slash];
        var relativePath = templateName[(slash + 1)..];

        if (!_bundlesInScope.Contains(bundleId) ||
            !_provider.TryGetBundleRoot(bundleId, out var root) ||
            string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var rootFullPath = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(rootFullPath, relativePath));
        if (!IsUnderRoot(rootFullPath, candidate) || !File.Exists(candidate))
        {
            return null;
        }

        return File.ReadAllText(candidate);
    }

    // DECISION: containment is textual (GetFullPath normalises '..' but does not
    // resolve symlinks). A symlink INSIDE a bundle root pointing outside would pass
    // — an accepted residual boundary under the curated/self-hosted trust model
    // (ADR-013). Harden with real-path resolution before accepting untrusted bundles.
    private static bool IsUnderRoot(string rootFullPath, string candidateFullPath)
    {
        var normalizedRoot = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(candidateFullPath, normalizedRoot, StringComparison.Ordinal) ||
               candidateFullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
