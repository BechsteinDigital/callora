namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Host view for one plugin descriptor.
/// </summary>
/// <param name="PluginId">Stable plugin identifier.</param>
/// <param name="DisplayName">Human-readable plugin name.</param>
/// <param name="AssemblyPath">Path the plugin assembly was loaded from.</param>
/// <param name="EntryTypeName">Optional entry type name, if the plugin declares one.</param>
/// <param name="State">Current lifecycle state.</param>
public sealed record HostPluginDescriptor(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    HostPluginState State);
