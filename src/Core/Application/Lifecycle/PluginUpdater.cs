using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Updates installed plugins from NuGet or local sources with rollback to the previous version on failure.
/// </summary>
public sealed class PluginUpdater(
    IHostPluginLifecycle lifecycle,
    INuGetPluginAssemblyResolver nuGetAssemblyResolver,
    ILocalPluginInstallSourceResolver? localPluginInstallSourceResolver,
    IPluginInstallationRepository installationRepository,
    PluginInstaller installer,
    PluginInstallationRecorder recorder,
    PluginLifecycleReporter reporter,
    PluginExtensionSynchronizer extensionSynchronizer)
{
    /// <summary>
    /// Updates one plugin from a NuGet package.
    /// </summary>
    public async Task<PluginLifecycleServiceResult> UpdateFromNuGetAsync(
        UpdateNuGetPluginCommand command,
        CancellationToken cancellationToken)
    {
        var resolved = await nuGetAssemblyResolver
            .ResolveAsync(command.PackageId, command.PackageVersion, command.AssemblyFileName, cancellationToken)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess || string.IsNullOrWhiteSpace(resolved.AssemblyPath))
        {
            var resolveMessage = resolved.Message ?? "NuGet package resolve failed.";
            var failureMetadata = new Dictionary<string, string>
            {
                ["source"] = "nuget",
                ["packageId"] = command.PackageId,
                ["packageVersion"] = command.PackageVersion,
                ["assemblyFileName"] = command.AssemblyFileName ?? string.Empty
            };

            await reporter.WriteAuditAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Update,
                        PluginId: command.PluginId,
                        IsSuccess: false,
                        RequestedBy: command.RequestedBy,
                        Message: resolveMessage,
                        Metadata: failureMetadata),
                    cancellationToken)
                .ConfigureAwait(false);
            await reporter.PublishEventAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Update,
                        PluginId: command.PluginId,
                        IsSuccess: false,
                        RequestedBy: command.RequestedBy,
                        Message: resolveMessage),
                    cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                command.PluginId,
                resolveMessage);
        }

        var metadata = new Dictionary<string, string>
        {
            ["source"] = "nuget",
            ["packageId"] = command.PackageId,
            ["packageVersion"] = command.PackageVersion,
            ["assemblyFileName"] = command.AssemblyFileName ?? string.Empty
        };

        return await UpdateFromResolvedAssemblyAsync(
                pluginId: command.PluginId,
                assemblyPath: resolved.AssemblyPath,
                requestedEntryTypeName: command.EntryTypeName,
                requestedBy: command.RequestedBy,
                updateMetadata: metadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Updates one plugin from a local plugin source directory.
    /// </summary>
    public async Task<PluginLifecycleServiceResult> UpdateFromLocalAsync(
        UpdateLocalPluginCommand command,
        CancellationToken cancellationToken)
    {
        if (localPluginInstallSourceResolver is null)
        {
            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                command.PluginId,
                "Local plugin updates are not available in this host configuration.");
        }

        var resolved = await localPluginInstallSourceResolver
            .ResolveForInstallAsync(command.PluginId, command.BuildIfNeeded, command.ForceBuild, cancellationToken)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess || string.IsNullOrWhiteSpace(resolved.AssemblyPath))
        {
            var resolveMessage = resolved.Message;
            var failureMetadata = new Dictionary<string, string>
            {
                ["source"] = "local",
                ["buildIfNeeded"] = command.BuildIfNeeded ? "true" : "false",
                ["forceBuild"] = command.ForceBuild ? "true" : "false"
            };

            await reporter.WriteAuditAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Update,
                        PluginId: command.PluginId,
                        IsSuccess: false,
                        RequestedBy: command.RequestedBy,
                        Message: resolveMessage,
                        Metadata: failureMetadata),
                    cancellationToken)
                .ConfigureAwait(false);
            await reporter.PublishEventAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Update,
                        PluginId: command.PluginId,
                        IsSuccess: false,
                        RequestedBy: command.RequestedBy,
                        Message: resolveMessage),
                    cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                command.PluginId,
                resolveMessage,
                resolved.ErrorCode);
        }

        var metadata = new Dictionary<string, string>
        {
            ["source"] = "local",
            ["buildIfNeeded"] = command.BuildIfNeeded ? "true" : "false",
            ["forceBuild"] = command.ForceBuild ? "true" : "false",
            ["usedBuild"] = resolved.UsedBuild ? "true" : "false"
        };

        return await UpdateFromResolvedAssemblyAsync(
                pluginId: command.PluginId,
                assemblyPath: resolved.AssemblyPath,
                requestedEntryTypeName: resolved.EntryTypeName,
                requestedBy: command.RequestedBy,
                updateMetadata: metadata,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> UpdateFromResolvedAssemblyAsync(
        string pluginId,
        string assemblyPath,
        string? requestedEntryTypeName,
        string? requestedBy,
        IReadOnlyDictionary<string, string> updateMetadata,
        CancellationToken cancellationToken)
    {
        var existingInstallation = await installationRepository
            .GetByPluginIdAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (existingInstallation is null || existingInstallation.State == PluginInstallationState.Uninstalled)
        {
            const string missingMessage = "Plugin update target is not installed.";
            var missingMetadata = new Dictionary<string, string>(updateMetadata)
            {
                ["reasonCode"] = PluginLifecycleErrorCodes.PluginUpdateTargetNotFound
            };

            await reporter.ReportAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Update,
                        PluginId: pluginId,
                        IsSuccess: false,
                        RequestedBy: requestedBy,
                        Message: missingMessage,
                        Metadata: missingMetadata),
                    cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                pluginId,
                missingMessage,
                PluginLifecycleErrorCodes.PluginUpdateTargetNotFound);
        }

        var previousAssemblyPath = existingInstallation.AssemblyPath;
        var previousEntryTypeName = existingInstallation.EntryTypeName;
        var previousDisplayName = existingInstallation.DisplayName;
        var previousState = existingInstallation.State;
        var wasActive = previousState == PluginInstallationState.Active;

        if (wasActive)
        {
            var deactivate = await lifecycle.DeactivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
            if (!deactivate.IsSuccess)
            {
                await reporter.ReportAsync(
                        new PluginLifecycleReport(
                            Action: PluginLifecycleActions.Update,
                            PluginId: pluginId,
                            IsSuccess: false,
                            RequestedBy: requestedBy,
                            Message: deactivate.Message),
                        cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    pluginId,
                    deactivate.Message);
            }
        }

        var uninstall = await lifecycle.UninstallAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (!uninstall.IsSuccess)
        {
            if (wasActive)
            {
                _ = await lifecycle.ActivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
            }

            await reporter.ReportAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Update,
                        PluginId: pluginId,
                        IsSuccess: false,
                        RequestedBy: requestedBy,
                        Message: uninstall.Message),
                    cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                pluginId,
                uninstall.Message);
        }

        var effectiveMetadata = new Dictionary<string, string>(updateMetadata)
        {
            ["previousAssemblyPath"] = previousAssemblyPath,
            ["resolvedAssemblyPath"] = assemblyPath
        };

        var install = await installer.InstallFromResolvedAssemblyAsync(
                assemblyPath: assemblyPath,
                requestedEntryTypeName: requestedEntryTypeName,
                requestedBy: requestedBy,
                sourceMetadata: effectiveMetadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (install.IsSuccess && string.Equals(install.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
        {
            if (wasActive)
            {
                var activate = await lifecycle.ActivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
                if (!activate.IsSuccess)
                {
                    return await RollbackAsync(
                            pluginId,
                            requestedBy,
                            previousAssemblyPath,
                            previousEntryTypeName,
                            previousDisplayName,
                            previousState,
                            rollbackTrigger: activate.Message ?? "Activation of updated plugin failed.",
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await recorder.MarkAsync(
                        pluginId: pluginId,
                        displayName: previousDisplayName,
                        assemblyPath: assemblyPath,
                        entryTypeName: requestedEntryTypeName ?? previousEntryTypeName,
                        mark: static (x, now) => x.MarkActivated(now),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            await reporter.ReportAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Update,
                        PluginId: pluginId,
                        IsSuccess: true,
                        RequestedBy: requestedBy,
                        Message: install.Message,
                        Metadata: effectiveMetadata),
                    cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Ok,
                true,
                pluginId,
                install.Message,
                WarningMessage: install.WarningMessage,
                WarningCode: install.WarningCode);
        }

        return await RollbackAsync(
                pluginId,
                requestedBy,
                previousAssemblyPath,
                previousEntryTypeName,
                previousDisplayName,
                previousState,
                rollbackTrigger: install.Message ?? "Plugin update install failed.",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> RollbackAsync(
        string pluginId,
        string? requestedBy,
        string previousAssemblyPath,
        string? previousEntryTypeName,
        string previousDisplayName,
        PluginInstallationState previousState,
        string rollbackTrigger,
        CancellationToken cancellationToken)
    {
        var rollbackMetadata = new Dictionary<string, string>
        {
            ["triggerAction"] = PluginLifecycleActions.Update,
            ["triggerMessage"] = rollbackTrigger,
            ["rollbackAssemblyPath"] = previousAssemblyPath
        };

        var rollbackInstall = await lifecycle
            .InstallAsync(previousAssemblyPath, previousEntryTypeName, cancellationToken)
            .ConfigureAwait(false);
        if (!rollbackInstall.IsSuccess)
        {
            await reporter.ReportAsync(
                    new PluginLifecycleReport(
                        Action: PluginLifecycleActions.Rollback,
                        PluginId: pluginId,
                        IsSuccess: false,
                        RequestedBy: requestedBy,
                        Message: rollbackInstall.Message,
                        Metadata: rollbackMetadata),
                    cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                pluginId,
                $"Plugin update failed and rollback failed: {rollbackInstall.Message}",
                PluginLifecycleErrorCodes.PluginRollbackFailed);
        }

        if (previousState == PluginInstallationState.Active)
        {
            var rollbackActivate = await lifecycle.ActivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
            if (!rollbackActivate.IsSuccess)
            {
                await reporter.ReportAsync(
                        new PluginLifecycleReport(
                            Action: PluginLifecycleActions.Rollback,
                            PluginId: pluginId,
                            IsSuccess: false,
                            RequestedBy: requestedBy,
                            Message: rollbackActivate.Message,
                            Metadata: rollbackMetadata),
                        cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    pluginId,
                    $"Plugin update failed and rollback activation failed: {rollbackActivate.Message}",
                    PluginLifecycleErrorCodes.PluginRollbackFailed);
            }

            var extensionSync = await extensionSynchronizer.SyncAsync(pluginId, cancellationToken).ConfigureAwait(false);
            if (!extensionSync.IsSuccess)
            {
                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    pluginId,
                    $"Plugin update failed and rollback extension sync failed: {extensionSync.Message}",
                    PluginLifecycleErrorCodes.PluginRollbackFailed);
            }
        }

        await recorder.RestoreAsync(
                pluginId,
                previousDisplayName,
                previousAssemblyPath,
                previousEntryTypeName,
                previousState,
                cancellationToken)
            .ConfigureAwait(false);

        await reporter.ReportAsync(
                new PluginLifecycleReport(
                    Action: PluginLifecycleActions.Rollback,
                    PluginId: pluginId,
                    IsSuccess: true,
                    RequestedBy: requestedBy,
                    Message: "Rollback restored previous stable plugin version.",
                    Metadata: rollbackMetadata),
                cancellationToken)
            .ConfigureAwait(false);

        await reporter.ReportAsync(
                new PluginLifecycleReport(
                    Action: PluginLifecycleActions.Update,
                    PluginId: pluginId,
                    IsSuccess: false,
                    RequestedBy: requestedBy,
                    Message: rollbackTrigger,
                    Metadata: new Dictionary<string, string>
                    {
                        ["rollbackTriggered"] = "true",
                        ["rollbackAction"] = PluginLifecycleActions.Rollback
                    }),
                cancellationToken)
            .ConfigureAwait(false);

        return new PluginLifecycleServiceResult(
            PluginLifecycleServiceStatus.BadRequest,
            false,
            pluginId,
            $"Plugin update failed. Rollback restored previous version: {rollbackTrigger}");
    }
}
