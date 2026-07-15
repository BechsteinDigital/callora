using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Host-facing lifecycle adapter over the runtime plugin host.
/// </summary>
public sealed class HostPluginLifecycle(ICalloraPluginRuntime runtime) : IHostPluginLifecycle
{
    /// <inheritdoc />
    public IReadOnlyCollection<HostPluginDescriptor> Plugins =>
        runtime.LoadedPlugins
            .Select(static plugin => new HostPluginDescriptor(
                plugin.PluginId,
                plugin.DisplayName,
                plugin.AssemblyPath,
                plugin.EntryTypeName,
                ToHostState(plugin.State)))
            .ToArray();

    /// <inheritdoc />
    public async Task<HostPluginOperationResult> InstallAsync(
        string assemblyPath,
        string? entryTypeName = null,
        CancellationToken cancellationToken = default)
    {
        var result = await runtime.InstallAsync(assemblyPath, entryTypeName, cancellationToken).ConfigureAwait(false);
        return new HostPluginOperationResult(
            HostPluginOperation.Install,
            result.IsSuccess,
            result.Plugin?.PluginId,
            result.Message);
    }

    /// <inheritdoc />
    public async Task<HostPluginOperationResult> ActivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var result = await runtime.ActivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
        return new HostPluginOperationResult(
            HostPluginOperation.Activate,
            result.IsSuccess,
            pluginId,
            result.Message);
    }

    /// <inheritdoc />
    public async Task<HostPluginOperationResult> DeactivateAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var result = await runtime.DeactivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
        return new HostPluginOperationResult(
            HostPluginOperation.Deactivate,
            result.IsSuccess,
            pluginId,
            result.Message);
    }

    /// <inheritdoc />
    public async Task<HostPluginOperationResult> UninstallAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var result = await runtime.UninstallAsync(pluginId, cancellationToken).ConfigureAwait(false);
        return new HostPluginOperationResult(
            HostPluginOperation.Uninstall,
            result.IsSuccess,
            pluginId,
            result.Message);
    }

    internal static HostPluginState ToHostState(RuntimePluginState state) =>
        state switch
        {
            RuntimePluginState.Active => HostPluginState.Active,
            RuntimePluginState.Inactive => HostPluginState.Inactive,
            RuntimePluginState.Faulted => HostPluginState.Faulted,
            RuntimePluginState.UnloadFailed => HostPluginState.UnloadFailed,
            _ => HostPluginState.Installed,
        };
}
