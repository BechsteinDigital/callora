using Microsoft.Extensions.Configuration;

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
    /// Read-only deployment configuration rooted at the current plugin's own
    /// section. A plugin never receives the host root configuration and
    /// therefore cannot read another plugin's settings or host secrets.
    /// </summary>
    IConfiguration? PluginConfiguration => null;

    /// <summary>
    /// Publishes one service instance for the provided contract type.
    /// </summary>
    void Export(Type contractType, object service);
}
