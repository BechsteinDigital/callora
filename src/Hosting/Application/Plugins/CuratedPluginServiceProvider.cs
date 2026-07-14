using Callora.Host.PluginContracts.Application.Data;
using Callora.Host.PluginContracts.Application.Persistence;
using Callora.Host.PluginContracts.Application.Plugins;
using Microsoft.Extensions.Logging;

namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Curated service surface for plugins (PLAT-252): resolves only published
/// contracts (PluginContracts assembly, Callora.Contracts.*) plus logging.
/// Everything else returns null — plugins cannot reach arbitrary host
/// services through the root provider anymore. IPluginDataStore is handed
/// out plugin-bound, so a plugin cannot address foreign plugin data.
/// </summary>
internal sealed class CuratedPluginServiceProvider(
    IServiceProvider rootServices,
    string pluginId) : IServiceProvider
{
    private static readonly string PluginContractsAssemblyName =
        typeof(IHostPluginContext).Assembly.GetName().Name!;

    public object? GetService(Type serviceType)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(IPluginDataStore))
        {
            return rootServices.GetService(typeof(IPluginDataStore)) is IPluginDataStore inner
                ? new PluginBoundDataStore(inner, pluginId)
                : null;
        }

        // IPluginDbContextFactory<TContext>: the plugin's own EF context on
        // the host database, in its dedicated schema (PLAT-260).
        if (serviceType.IsGenericType &&
            serviceType.GetGenericTypeDefinition() == typeof(IPluginDbContextFactory<>))
        {
            if (rootServices.GetService(typeof(IPluginDbContextProvider)) is not IPluginDbContextProvider provider)
            {
                return null;
            }

            var contextType = serviceType.GetGenericArguments()[0];
            var factoryType = typeof(PluginDbContextFactory<>).MakeGenericType(contextType);
            return Activator.CreateInstance(factoryType, provider, pluginId);
        }

        return IsAllowed(serviceType) ? rootServices.GetService(serviceType) : null;
    }

    private static bool IsAllowed(Type serviceType)
    {
        if (serviceType == typeof(ILoggerFactory))
        {
            return true;
        }

        if (serviceType.IsGenericType && serviceType.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            return true;
        }

        var assemblyName = serviceType.Assembly.GetName().Name;
        return string.Equals(assemblyName, PluginContractsAssemblyName, StringComparison.Ordinal) ||
               (assemblyName is not null && assemblyName.StartsWith("Callora.Contracts.", StringComparison.Ordinal));
    }
}
