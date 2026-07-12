using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Hosting.Application.Plugins;

internal sealed class PluginContext(
    IServiceProvider services,
    string pluginId,
    Action<string, Type, object> registerExport) : IHostPluginContext
{
    public IServiceProvider Services { get; } = services;

    public void Export(Type contractType, object service)
    {
        registerExport(pluginId, contractType, service);
    }
}
