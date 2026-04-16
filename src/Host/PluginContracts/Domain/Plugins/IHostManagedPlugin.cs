using VoipHost.PluginContracts.Application.Plugins;

namespace VoipHost.PluginContracts.Domain.Plugins;

/// <summary>
/// Runtime-loadable plugin entrypoint owned by the host platform.
/// </summary>
public interface IHostManagedPlugin
{
    /// <summary>Stable plugin identifier.</summary>
    string PluginId { get; }

    /// <summary>Display name shown by host tooling.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Starts the plugin and registers runtime exports.
    /// </summary>
    ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the plugin and releases runtime resources.
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
