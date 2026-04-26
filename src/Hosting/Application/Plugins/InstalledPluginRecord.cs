using Callora.Modules.Abstractions.Application.Plugins;

namespace Callora.Hosting.Application.Plugins;

internal sealed record InstalledPluginRecord(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    RuntimePluginState State)
{
    public RuntimePluginState State { get; set; } = State;
}
