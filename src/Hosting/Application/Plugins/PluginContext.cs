using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Hosting.Application.Plugins;

internal sealed class PluginContext(
    IServiceProvider services,
    string pluginId,
    Action<string, Type, object> registerExport) : IHostPluginContext
{
    // Kuratierte Oberfläche statt Root-Provider: Plugins sehen nur
    // veröffentlichte Verträge und plugin-gebundene Dienste (PLAT-252).
    public IServiceProvider Services { get; } = new CuratedPluginServiceProvider(services, pluginId);

    public void Export(Type contractType, object service)
    {
        registerExport(pluginId, contractType, service);
    }
}
