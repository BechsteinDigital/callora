namespace Callora.Core.Infrastructure.Plugins;

public sealed record PluginUiAssetManifestEntry(
    string PluginId,
    string Surface,
    string EntryPath)
{
    /// <summary>
    /// Short content hash of the built entry file. The client loader appends it as a
    /// cache-busting <c>?v=</c> query, so an upgraded bundle becomes a new URL and can
    /// never be served stale from cache. Null when the file could not be hashed — the
    /// client then loads the bare path and relies on revalidation.
    /// </summary>
    public string? ContentHash { get; init; }
}
