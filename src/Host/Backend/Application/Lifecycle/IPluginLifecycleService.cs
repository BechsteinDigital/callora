using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

public interface IPluginLifecycleService
{
    IReadOnlyCollection<HostPluginDescriptor> Plugins { get; }

    Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(
        CancellationToken cancellationToken = default);

    Task<PluginLifecycleServiceResult> InstallAsync(
        InstallPluginCommand command,
        CancellationToken cancellationToken = default);

    Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken = default);

    Task<PluginLifecycleServiceResult> UpdateFromNuGetAsync(
        UpdateNuGetPluginCommand command,
        CancellationToken cancellationToken = default);

    Task<PluginLifecycleServiceResult> UpdateFromLocalAsync(
        UpdateLocalPluginCommand command,
        CancellationToken cancellationToken = default);

    Task<PluginLifecycleServiceResult> ActivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default);

    Task<PluginLifecycleServiceResult> DeactivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default);

    Task<PluginLifecycleServiceResult> UninstallAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default);
}
