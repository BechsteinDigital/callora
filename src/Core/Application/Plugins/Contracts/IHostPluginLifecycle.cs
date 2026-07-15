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

    Task<HostPluginOperationResult> InstallAsync(
        string assemblyPath,
        string? entryTypeName = null,
        CancellationToken cancellationToken = default);

    Task<HostPluginOperationResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    Task<HostPluginOperationResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    Task<HostPluginOperationResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default);
}
