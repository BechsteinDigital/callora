namespace Callora.Modules.Abstractions.Application.Plugins;

/// <summary>
/// Runtime-loadable plugin entrypoint.
/// </summary>
public interface ICalloraRuntimePlugin
{
    /// <summary>Stable plugin identifier.</summary>
    string PluginId { get; }

    /// <summary>Display name shown by host tooling.</summary>
    string DisplayName { get; }

    /// <summary>
    /// Starts the plugin and registers runtime exports.
    /// </summary>
    ValueTask StartAsync(ICalloraPluginContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the plugin and releases runtime resources.
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
