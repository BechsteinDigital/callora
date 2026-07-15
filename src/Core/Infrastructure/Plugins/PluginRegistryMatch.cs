namespace Callora.Core.Infrastructure.Plugins;

public sealed record PluginRegistryMatch(
    string RegistryPath,
    PluginRegistryJsonDto Registry);
