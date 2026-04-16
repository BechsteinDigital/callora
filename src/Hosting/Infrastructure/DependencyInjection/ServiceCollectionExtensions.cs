using Callora.Hosting.Application.Bootstrap;
using Callora.Hosting.Application.Options;
using Callora.Hosting.Application.Plugins;
using Callora.Hosting.Application.Startup;
using VoipHost.PluginContracts.Application.Plugins;
using Callora.Modules.Abstractions.Application.Plugins;

namespace Callora.Hosting.Infrastructure.DependencyInjection;

/// <summary>
/// Dependency injection registration contract for Callora hosting utilities.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Callora hosting support in a generic service registry.
    /// </summary>
    public static void AddCalloraHosting(IHostServiceRegistry services, Action<CalloraHostingOptions>? configure = null)
    {
        var options = new CalloraHostingOptions();
        configure?.Invoke(options);

        services.AddSingleton(typeof(CalloraHostingOptions), options);
        services.AddSingleton(typeof(ModuleBootstrapRunner), typeof(ModuleBootstrapRunner));
        services.AddSingleton(typeof(RuntimePluginHost), typeof(RuntimePluginHost));
        services.AddSingleton(typeof(ICalloraPluginRuntime), typeof(RuntimePluginHost));
        services.AddSingleton(typeof(ICalloraPluginCatalog), typeof(RuntimePluginHost));
        services.AddSingleton(typeof(IHostPluginLifecycle), typeof(HostPluginLifecycle));
        services.AddSingleton(typeof(CalloraHostStartup), typeof(CalloraHostStartup));
    }
}
