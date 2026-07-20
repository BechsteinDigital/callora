namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// One published plugin stylesheet; shells load these in template-chain order
/// before the plugin scripts so themes can override base styles.
/// </summary>
public sealed record PluginUiStyleManifestEntry(string PluginId, string Surface, string StylePath)
{
    /// <summary>
    /// Short content hash of the built stylesheet, used by the client loader as a
    /// cache-busting <c>?v=</c> query. Null when the file could not be hashed.
    /// </summary>
    public string? ContentHash { get; init; }
}
