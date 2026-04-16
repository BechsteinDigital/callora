using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class FakeHostPluginLifecycle : IHostPluginLifecycle
{
    public IReadOnlyCollection<HostPluginDescriptor> Plugins { get; set; } = [];

    public HostPluginOperationResult InstallResult { get; set; } =
        new(HostPluginOperation.Install, true, "plugin-1", null);

    public HostPluginOperationResult ActivateResult { get; set; } =
        new(HostPluginOperation.Activate, true, "plugin-1", null);

    public HostPluginOperationResult DeactivateResult { get; set; } =
        new(HostPluginOperation.Deactivate, true, "plugin-1", null);

    public HostPluginOperationResult UninstallResult { get; set; } =
        new(HostPluginOperation.Uninstall, true, "plugin-1", null);

    public string? LastInstallAssemblyPath { get; private set; }

    public string? LastInstallEntryTypeName { get; private set; }

    public int InstallCallCount { get; private set; }

    public Task<HostPluginOperationResult> InstallAsync(
        string assemblyPath,
        string? entryTypeName = null,
        CancellationToken cancellationToken = default)
    {
        InstallCallCount++;
        LastInstallAssemblyPath = assemblyPath;
        LastInstallEntryTypeName = entryTypeName;
        return Task.FromResult(InstallResult);
    }

    public Task<HostPluginOperationResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ActivateResult with { PluginId = pluginId });

    public Task<HostPluginOperationResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(DeactivateResult with { PluginId = pluginId });

    public Task<HostPluginOperationResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(UninstallResult with { PluginId = pluginId });
}
