using Microsoft.AspNetCore.Hosting;

namespace Callora.Surface.Rendering;

/// <summary>
/// The distribution-wired <see cref="ISurfaceTemplateBundleProvider"/>: it maps a
/// plugin id to that plugin's PUBLISHED workspace views root — the tree the UI asset
/// publisher lays down at <c>&lt;webRoot&gt;/plugin-assets/&lt;id&gt;/views/workspace</c>
/// (see PluginUiAssetPublisher). The renderer resolves relative
/// <c>extends</c>/<c>include</c> names, and cross-bundle <c>@id/path</c> names, against
/// these roots; it also reads the surface ENTRY template of a chain's primary plugin.
/// Every resolved path is confined under the plugin-assets root, so a crafted id can
/// never escape it (defence in depth on top of the loader's own confinement).
/// </summary>
public sealed class PublishedSurfaceTemplateBundles : ISurfaceTemplateBundleProvider
{
    // Only the workspace surface publishes SSR templates today: PluginUiAssetPublisher
    // publishes plugin views under views/workspace. Generalise to a surface argument
    // when an admin SSR surface lands.
    private const string ViewsSurfaceSegment = "surface";

    // The SSR entry, mirroring the built-JS entry convention (index.js/main.js): the
    // primary template plugin's root document. .njk is the engine's native extension;
    // .html files stay reserved for the client-loaded manifest side.
    private static readonly string[] EntryCandidates = ["index.njk", "main.njk"];

    private readonly IWebHostEnvironment _environment;

    public PublishedSurfaceTemplateBundles(IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    public bool TryGetBundleRoot(string bundleId, out string? rootFullPath)
    {
        var root = ResolveConfinedRoot(bundleId);
        if (root is null || !Directory.Exists(root))
        {
            rootFullPath = null;
            return false;
        }

        rootFullPath = root;
        return true;
    }

    /// <summary>
    /// Reads the surface entry template (<c>index.njk</c>, then <c>main.njk</c>) from the
    /// plugin's published workspace views root, or null when the plugin publishes none —
    /// the caller then falls back to the built-in SPA shell.
    /// </summary>
    public string? TryReadEntryTemplate(string bundleId)
    {
        var root = ResolveConfinedRoot(bundleId);
        if (root is null)
        {
            return null;
        }

        foreach (var candidate in EntryCandidates)
        {
            // The candidate is a fixed file name (no separators) under the already
            // confined root; re-verify containment and existence before reading.
            var candidatePath = Path.GetFullPath(Path.Combine(root, candidate));
            if (candidatePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                File.Exists(candidatePath))
            {
                return File.ReadAllText(candidatePath);
            }
        }

        return null;
    }

    // Resolves bundleId to <webRoot>/plugin-assets/<id>/views/workspace, confined under
    // the plugin-assets root. Returns null for a blank id or one that escapes the root
    // (e.g. "../x"). Existence is NOT checked here — callers decide what a missing
    // directory/file means. Mirrors the publisher's ResolveContainedTargetDirectory.
    private string? ResolveConfinedRoot(string bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId))
        {
            return null;
        }

        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot")
            : _environment.WebRootPath;
        var assetsRoot = Path.GetFullPath(Path.Combine(webRoot, "plugin-assets"));
        var candidate = Path.GetFullPath(Path.Combine(assetsRoot, bundleId, "views", ViewsSurfaceSegment));
        if (!candidate.StartsWith(assetsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return candidate;
    }
}
