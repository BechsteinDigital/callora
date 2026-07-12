namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Lightweight descriptor for one loaded plugin.
/// </summary>
public sealed record RuntimePluginDescriptor(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    RuntimePluginState State);
