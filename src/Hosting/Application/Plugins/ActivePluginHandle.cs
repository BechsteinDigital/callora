using VoipHost.PluginContracts.Application.Plugins;
using VoipHost.PluginContracts.Domain.Plugins;

namespace Callora.Hosting.Application.Plugins;

internal sealed record ActivePluginHandle(
    string PluginId,
    IHostManagedPlugin Plugin,
    PluginAssemblyLoadContext LoadContext);
