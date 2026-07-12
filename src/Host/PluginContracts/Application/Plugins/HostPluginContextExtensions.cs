namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// Convenience helpers for typed plugin export registration.
/// </summary>
public static class HostPluginContextExtensions
{
    /// <summary>
    /// Publishes one typed service instance.
    /// </summary>
    public static void Export<TContract>(this IHostPluginContext context, TContract service)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(service);
        context.Export(typeof(TContract), service);
    }
}
