using Callora.Host.Backend.Application.Lifecycle;
using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

public sealed class RecordingPluginLifecycleService : IPluginLifecycleService
{
    public List<InstallPluginCommand> InstallCalls { get; } = [];

    public List<PluginLifecycleCommand> ActivateCalls { get; } = [];

    public IReadOnlyCollection<HostPluginDescriptor> Plugins { get; } = Array.Empty<HostPluginDescriptor>();

    public Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PluginInstallationSnapshot>>(Array.Empty<PluginInstallationSnapshot>());

    public Task<PluginLifecycleServiceResult> InstallAsync(
        InstallPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        InstallCalls.Add(command);
        return Task.FromResult(new PluginLifecycleServiceResult(
            PluginLifecycleServiceStatus.Ok,
            true,
            PluginId: "voip",
            Message: "installed"));
    }

    public Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false));

    public Task<PluginLifecycleServiceResult> UpdateFromNuGetAsync(
        UpdateNuGetPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false));

    public Task<PluginLifecycleServiceResult> UpdateFromLocalAsync(
        UpdateLocalPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false));

    public Task<PluginLifecycleServiceResult> ActivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        ActivateCalls.Add(command);
        return Task.FromResult(new PluginLifecycleServiceResult(
            PluginLifecycleServiceStatus.Ok,
            true,
            PluginId: command.PluginId,
            Message: "activated"));
    }

    public Task<PluginLifecycleServiceResult> DeactivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false));

    public Task<PluginLifecycleServiceResult> UninstallAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false));
}
