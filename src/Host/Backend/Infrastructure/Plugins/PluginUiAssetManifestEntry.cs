namespace Callora.Host.Backend.Infrastructure.Plugins;

public sealed record PluginUiAssetManifestEntry(
    string PluginId,
    string Surface,
    string EntryPath);
