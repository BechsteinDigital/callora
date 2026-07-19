namespace Callora.Surface.Rendering;

/// <summary>
/// Maps a template bundle id to the physical root directory that holds its view
/// files. The rendering layer resolves <c>@bundleId/relative/path.html</c>
/// includes against these roots; a concrete provider binds bundle ids to the
/// installed template plugins (a later wiring phase).
/// </summary>
public interface ISurfaceTemplateBundleProvider
{
    /// <summary>
    /// Resolves a bundle id to its absolute root directory. Returns false for an
    /// unknown bundle; when true, <paramref name="rootFullPath"/> is the directory
    /// under which the bundle's view files live (path-confined by the loader).
    /// </summary>
    bool TryGetBundleRoot(string bundleId, out string? rootFullPath);
}
