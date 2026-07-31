using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Tests.Support;

internal sealed class ConfigurablePluginLifecycleService : IPluginLifecycleService
{
    public IReadOnlyCollection<HostPluginDescriptor> Plugins { get; } = [];

    public List<PluginInstallationSnapshot> Installations { get; } = [];

    public List<PluginLifecycleCommand> ActivateCalls { get; } = [];

    public List<PluginLifecycleCommand> DeactivateCalls { get; } = [];

    public PluginLifecycleServiceResult ActivateResult { get; set; } =
        new(PluginLifecycleServiceStatus.Ok, true);

    public PluginLifecycleServiceResult DeactivateResult { get; set; } =
        new(PluginLifecycleServiceStatus.Ok, true);

    public Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PluginInstallationSnapshot>>(Installations);

    public Task<PluginLifecycleServiceResult> ActivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        ActivateCalls.Add(command);
        return Task.FromResult(ActivateResult with { PluginId = command.PluginId });
    }

    public Task<PluginLifecycleServiceResult> DeactivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        DeactivateCalls.Add(command);
        return Task.FromResult(DeactivateResult with { PluginId = command.PluginId });
    }

    public Task<PluginLifecycleServiceResult> InstallAsync(
        InstallPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false));

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

    public Task<PluginLifecycleServiceResult> UninstallAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false));
}
