using Callora.Core.Application.Data.Contracts;
using Callora.Core.Application.Persistence.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Curated service surface for plugins (PLAT-252): resolves only published
/// contracts (the Callora.Core.*.Contracts namespaces, Callora.Contracts.*,
/// foundation *.Abstractions packages) plus logging. Since the former
/// PluginContracts assembly merged feature-first into the fat Core, the
/// namespace — not the assembly — separates a published contract from a host
/// internal (REV2 §7). A contract the host does not register itself is resolved
/// from a cross-plugin export (REV2 §9.3 "geteilte Service-Exports"), so e.g.
/// the Communication plugin can provide ICommunicationChannelRegistry to the
/// Dialer plugin. Everything else returns null — plugins cannot reach arbitrary
/// host services through the root provider. IPluginDataStore is handed out
/// plugin-bound, so a plugin cannot address foreign plugin data.
/// </summary>
internal sealed class CuratedPluginServiceProvider(
    IServiceProvider rootServices,
    string pluginId,
    Func<Type, object?>? resolveExport = null) : IServiceProvider
{
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

        if (!IsAllowed(serviceType))
        {
            return null;
        }

        // Host registration wins; otherwise fall back to a cross-plugin export
        // (e.g. the Communication plugin's ICommunicationChannelRegistry).
        return rootServices.GetService(serviceType) ?? resolveExport?.Invoke(serviceType);
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

        // Published contracts moved feature-first into Core and now live in
        // Callora.Core.<Layer>.<Feature>.Contracts namespaces, next to their host
        // implementations. The namespace is the boundary — the assembly is the
        // whole fat Core and no longer discriminates (REV2 §7 "Freiheit über
        // Schutz"; a later analyzer enforces it).
        var ns = serviceType.Namespace;
        if (ns is not null &&
            ns.StartsWith("Callora.Core.", StringComparison.Ordinal) &&
            (ns.EndsWith(".Contracts", StringComparison.Ordinal) ||
             ns.Contains(".Contracts.", StringComparison.Ordinal)))
        {
            return true;
        }

        var assemblyName = serviceType.Assembly.GetName().Name;
        if (assemblyName is null)
        {
            return false;
        }

        return assemblyName.StartsWith("Callora.Contracts.", StringComparison.Ordinal) ||
               // Foundation contract packages (e.g. Callora.Plugin.Communication.Abstractions)
               // are unified in the shared load context and published cross-plugin.
               (assemblyName.StartsWith("Callora.Plugin.", StringComparison.Ordinal) &&
                assemblyName.EndsWith(".Abstractions", StringComparison.Ordinal));
    }
}
