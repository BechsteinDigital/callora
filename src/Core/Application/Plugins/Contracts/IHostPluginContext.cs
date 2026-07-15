namespace Callora.Core.Application.Plugins.Contracts;

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
