using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Startup;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Infrastructure.DependencyInjection;

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
        services.AddSingleton(typeof(RuntimePluginHost), typeof(RuntimePluginHost));
        services.AddSingleton(typeof(ICalloraPluginRuntime), provider =>
            (ICalloraPluginRuntime)(provider.GetService(typeof(RuntimePluginHost))
                ?? throw new InvalidOperationException("RuntimePluginHost is not registered.")));
        services.AddSingleton(typeof(ICalloraPluginCatalog), provider =>
            (ICalloraPluginCatalog)(provider.GetService(typeof(RuntimePluginHost))
                ?? throw new InvalidOperationException("RuntimePluginHost is not registered.")));
        services.AddSingleton(typeof(IHostPluginLifecycle), typeof(HostPluginLifecycle));
        services.AddSingleton(typeof(CalloraHostStartup), typeof(CalloraHostStartup));
    }
}
