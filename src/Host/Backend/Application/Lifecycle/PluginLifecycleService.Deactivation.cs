using System.Collections.Generic;

namespace Callora.Host.Backend.Application.Lifecycle;

public sealed partial class PluginLifecycleService
{
    public async Task<PluginLifecycleServiceResult> DeactivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInstrumentedLifecycleAsync(
                action: "deactivate",
                pluginId: command.PluginId,
                requestedBy: command.RequestedBy,
                workspaceKey: command.WorkspaceKey,
                executeAsync: token => DeactivateCoreAsync(command, token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> DeactivateCoreAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return await SetWorkspaceScopedActivationAsync(
                    command.PluginId,
                    command.WorkspaceKey,
                    isActive: false,
                    command.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var result = await lifecycle.DeactivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await WritePluginAuditAsync(
                action: "plugin.deactivate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.deactivate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                metadata: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await entitlementStore.ClearForPluginAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
            await extensionRegistrationStore.RemoveAsync(command.PluginId, cancellationToken).ConfigureAwait(false);

            var descriptor = FindDescriptor(command.PluginId);
            await UpsertInstallationAsync(
                    pluginId: command.PluginId,
                    displayName: descriptor?.DisplayName ?? command.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? string.Empty,
                    entryTypeName: descriptor?.EntryTypeName,
                    mark: static (x, now) => x.MarkDeactivated(now),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.Ok, true, command.PluginId, result.Message)
            : new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, command.PluginId, result.Message);
    }

    public async Task<PluginLifecycleServiceResult> UninstallAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInstrumentedLifecycleAsync(
                action: "uninstall",
                pluginId: command.PluginId,
                requestedBy: command.RequestedBy,
                workspaceKey: command.WorkspaceKey,
                executeAsync: token => UninstallCoreAsync(command, token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> UninstallCoreAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        var result = await lifecycle.UninstallAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await WritePluginAuditAsync(
                action: "plugin.uninstall",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.uninstall",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                metadata: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await entitlementStore.ClearForPluginAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
            await extensionRegistrationStore.RemoveAsync(command.PluginId, cancellationToken).ConfigureAwait(false);

            var descriptor = FindDescriptor(command.PluginId);
            await UpsertInstallationAsync(
                    pluginId: command.PluginId,
                    displayName: descriptor?.DisplayName ?? command.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? string.Empty,
                    entryTypeName: descriptor?.EntryTypeName,
                    mark: static (x, now) => x.MarkUninstalled(now),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.Ok, true, command.PluginId, result.Message)
            : new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, command.PluginId, result.Message);
    }


}
