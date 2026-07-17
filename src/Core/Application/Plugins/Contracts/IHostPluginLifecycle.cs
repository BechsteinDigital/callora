namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Host-side plugin lifecycle facade.
/// </summary>
public interface IHostPluginLifecycle
{
    /// <summary>
    /// Snapshot of currently known plugins.
    /// </summary>
    IReadOnlyCollection<HostPluginDescriptor> Plugins { get; }

    /// <summary>
    /// Loads the plugin from the given assembly path into its own load context and
    /// records it as installed. The plugin does not serve until it is activated.
    /// </summary>
    Task<HostPluginOperationResult> InstallAsync(
        string assemblyPath,
        string? entryTypeName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts an installed plugin so its exports become available to the host.
    /// </summary>
    Task<HostPluginOperationResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops an active plugin and withdraws its exports. The assembly load context
    /// may remain pinned until the next host restart (see <see cref="HostPluginState.UnloadFailed"/>).
    /// </summary>
    Task<HostPluginOperationResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the plugin if needed and removes it from the known set.
    /// </summary>
    Task<HostPluginOperationResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
