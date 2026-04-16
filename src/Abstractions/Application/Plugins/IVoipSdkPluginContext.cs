using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Modules.Abstractions.Application.Plugins;

/// <summary>
/// Runtime context passed to plugins during startup.
/// </summary>
public interface ICalloraPluginContext : IHostPluginContext
{
}

/// <summary>
/// Convenience helpers for typed plugin export registration.
/// </summary>
public static class CalloraPluginContextExtensions
{
    /// <summary>
    /// Publishes one typed service instance.
    /// </summary>
    public static void Export<TContract>(this ICalloraPluginContext context, TContract service)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(service);
        context.Export(typeof(TContract), service);
    }
}
