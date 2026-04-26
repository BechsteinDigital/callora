namespace Callora.Host.Backend.Infrastructure.Plugins;

public sealed record PluginRegistryMatch(
    string RegistryPath,
    PluginRegistryJsonDto Registry);
