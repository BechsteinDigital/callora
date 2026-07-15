using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;

namespace Callora.Core.Application.Plugins;

internal sealed record ActivePluginHandle(
    string PluginId,
    IHostManagedPlugin Plugin,
    PluginAssemblyLoadContext LoadContext);
