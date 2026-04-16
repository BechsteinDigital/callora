namespace VoipHost.PluginContracts.Application.Plugins;

/// <summary>
/// Runtime context passed to host-managed plugins during startup.
/// </summary>
public interface IHostPluginContext
{
    /// <summary>
    /// Application service provider owned by the host process.
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Publishes one service instance for the provided contract type.
    /// </summary>
    void Export(Type contractType, object service);
}

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
