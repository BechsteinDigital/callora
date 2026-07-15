namespace Callora.Core.Infrastructure.Plugins;

/// <summary>
/// One published plugin stylesheet; shells load these in template-chain order
/// before the plugin scripts so themes can override base styles.
/// </summary>
public sealed record PluginUiStyleManifestEntry(string PluginId, string Surface, string StylePath);
