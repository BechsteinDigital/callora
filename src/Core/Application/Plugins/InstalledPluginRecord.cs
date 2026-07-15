
namespace Callora.Core.Application.Plugins;

internal sealed record InstalledPluginRecord(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    RuntimePluginState State)
{
    public RuntimePluginState State { get; set; } = State;
}
