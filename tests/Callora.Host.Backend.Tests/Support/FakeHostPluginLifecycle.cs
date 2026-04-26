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

    public List<string> InstalledAssemblyPaths { get; } = [];

    public int InstallCallCount { get; private set; }

    public int ActivateCallCount { get; private set; }

    public int DeactivateCallCount { get; private set; }

    public int UninstallCallCount { get; private set; }

    public Func<string, string?, HostPluginOperationResult>? InstallHandler { get; set; }

    public Func<string, HostPluginOperationResult>? ActivateHandler { get; set; }

    public Func<string, HostPluginOperationResult>? DeactivateHandler { get; set; }

    public Func<string, HostPluginOperationResult>? UninstallHandler { get; set; }

    public Task<HostPluginOperationResult> InstallAsync(
        string assemblyPath,
        string? entryTypeName = null,
        CancellationToken cancellationToken = default)
    {
        InstallCallCount++;
        LastInstallAssemblyPath = assemblyPath;
        LastInstallEntryTypeName = entryTypeName;
        InstalledAssemblyPaths.Add(assemblyPath);
        if (InstallHandler is not null)
            return Task.FromResult(InstallHandler(assemblyPath, entryTypeName));

        return Task.FromResult(InstallResult);
    }

    public Task<HostPluginOperationResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        ActivateCallCount++;
        if (ActivateHandler is not null)
            return Task.FromResult(ActivateHandler(pluginId));

        return Task.FromResult(ActivateResult with { PluginId = pluginId });
    }

    public Task<HostPluginOperationResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        DeactivateCallCount++;
        if (DeactivateHandler is not null)
            return Task.FromResult(DeactivateHandler(pluginId));

        return Task.FromResult(DeactivateResult with { PluginId = pluginId });
    }

    public Task<HostPluginOperationResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        UninstallCallCount++;
        if (UninstallHandler is not null)
            return Task.FromResult(UninstallHandler(pluginId));

        return Task.FromResult(UninstallResult with { PluginId = pluginId });
    }
}
