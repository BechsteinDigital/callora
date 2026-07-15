namespace Callora.Core.Application.Plugins;

/// <summary>
/// Runtime plugin host facade with install/activate/deactivate/uninstall lifecycle operations.
/// </summary>
public interface ICalloraPluginRuntime : ICalloraPluginCatalog
{
    /// <summary>
    /// Snapshot of currently loaded plugins.
    /// </summary>
    IReadOnlyCollection<RuntimePluginDescriptor> LoadedPlugins { get; }

    /// <summary>
    /// Loads one plugin assembly into an isolated runtime context.
    /// </summary>
    Task<RuntimePluginInstallResult> InstallAsync(
        string assemblyPath,
        string? entryTypeName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates one previously installed plugin.
    /// </summary>
    Task<RuntimePluginActivateResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates one active plugin while keeping it installed.
    /// </summary>
    Task<RuntimePluginDeactivateResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls one previously installed plugin.
    /// </summary>
    Task<RuntimePluginUninstallResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
