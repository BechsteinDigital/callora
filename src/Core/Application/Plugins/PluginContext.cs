using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Application.Plugins;

internal sealed class PluginContext(
    IServiceProvider services,
    string pluginId,
    Action<string, Type, object> registerExport,
    Func<Type, object?> resolveExport) : IHostPluginContext
{
    // Kuratierte Oberfläche statt Root-Provider: Plugins sehen nur
    // veröffentlichte Verträge, plugin-gebundene Dienste (PLAT-252) und
    // plugin-übergreifend exportierte Contract-Services (REV2 §9.3).
    public IServiceProvider Services { get; } = new CuratedPluginServiceProvider(services, pluginId, resolveExport);

    public void Export(Type contractType, object service)
    {
        registerExport(pluginId, contractType, service);
    }
}
