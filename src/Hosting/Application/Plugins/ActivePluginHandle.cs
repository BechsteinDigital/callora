using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Host.PluginContracts.Domain.Plugins;

namespace Callora.Hosting.Application.Plugins;

internal sealed record ActivePluginHandle(
    string PluginId,
    IHostManagedPlugin Plugin,
    PluginAssemblyLoadContext LoadContext);
