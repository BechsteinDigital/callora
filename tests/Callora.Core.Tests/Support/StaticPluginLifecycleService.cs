using Callora.Core.Application.Lifecycle;
using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Core.Tests.Support;

internal sealed class StaticPluginLifecycleService : IPluginLifecycleService
{
    public IReadOnlyCollection<HostPluginDescriptor> Plugins { get; } = Array.Empty<HostPluginDescriptor>();

    public Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PluginInstallationSnapshot>>(Array.Empty<PluginInstallationSnapshot>());

    public Task<PluginLifecycleServiceResult> InstallAsync(
        InstallPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, Message: "not implemented"));

    public Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, Message: "not implemented"));

    public Task<PluginLifecycleServiceResult> UpdateFromNuGetAsync(
        UpdateNuGetPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, Message: "not implemented"));

    public Task<PluginLifecycleServiceResult> UpdateFromLocalAsync(
        UpdateLocalPluginCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, Message: "not implemented"));

    public Task<PluginLifecycleServiceResult> ActivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, Message: "not implemented"));

    public Task<PluginLifecycleServiceResult> DeactivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, Message: "not implemented"));

    public Task<PluginLifecycleServiceResult> UninstallAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, Message: "not implemented"));
}
