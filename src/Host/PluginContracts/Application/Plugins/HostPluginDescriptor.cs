namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// Host view for one plugin descriptor.
/// </summary>
public sealed record HostPluginDescriptor(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    HostPluginState State);
