using System.Collections.Concurrent;
using System.Diagnostics;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Application.Audit;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Domain.Extensions;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Modules.Abstractions.Application.Plugins;
using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

public sealed partial class PluginLifecycleService(
    IHostPluginLifecycle lifecycle,
    IPluginActivationPolicy activationPolicy,
    IPluginEntitlementStore entitlementStore,
    IHostAuditStore auditStore,
    IPluginInstallationRepository installationRepository,
    IHostUnitOfWork unitOfWork,
    IPluginPackageRegistryReader packageRegistryReader,
    IPluginPackageSignatureVerifier packageSignatureVerifier,
    INuGetPluginAssemblyResolver nuGetAssemblyResolver,
    IExtensionPointRegistryStore extensionPointRegistryStore,
    IPluginExtensionRegistrationStore extensionRegistrationStore,
    IHostApplicationEventPublisher eventPublisher,
    IWorkspaceManagementStore workspaceStore,
    ICalloraPluginCatalog? pluginCatalog = null,
    ILocalPluginInstallSourceResolver? localPluginInstallSourceResolver = null) : IPluginLifecycleService
{
    private readonly ConcurrentDictionary<string, WorkspaceLifecycleLock> _workspaceLifecycleLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ICalloraPluginCatalog _pluginCatalog = pluginCatalog ?? EmptyCalloraPluginCatalog.Instance;

    public PluginLifecycleService(
        IHostPluginLifecycle lifecycle,
        IPluginActivationPolicy activationPolicy,
        IPluginEntitlementStore entitlementStore,
        IHostAuditStore auditStore,
        IPluginInstallationRepository installationRepository,
        IHostUnitOfWork unitOfWork,
        IPluginPackageRegistryReader packageRegistryReader,
        IPluginPackageSignatureVerifier packageSignatureVerifier,
        INuGetPluginAssemblyResolver nuGetAssemblyResolver,
        IHostApplicationEventPublisher eventPublisher,
        ILocalPluginInstallSourceResolver? localPluginInstallSourceResolver = null)
        : this(
            lifecycle,
            activationPolicy,
            entitlementStore,
            auditStore,
            installationRepository,
            unitOfWork,
            packageRegistryReader,
            packageSignatureVerifier,
            nuGetAssemblyResolver,
            new EmptyExtensionPointRegistryStore(),
            new EmptyPluginExtensionRegistrationStore(),
            eventPublisher,
            new EmptyWorkspaceManagementStore(),
            localPluginInstallSourceResolver: localPluginInstallSourceResolver)
    {
    }

    public PluginLifecycleService(
        IHostPluginLifecycle lifecycle,
        IPluginActivationPolicy activationPolicy,
        IPluginEntitlementStore entitlementStore,
        IHostAuditStore auditStore,
        IPluginInstallationRepository installationRepository,
        IHostUnitOfWork unitOfWork,
        IPluginPackageRegistryReader packageRegistryReader,
        IPluginPackageSignatureVerifier packageSignatureVerifier,
        INuGetPluginAssemblyResolver nuGetAssemblyResolver,
        IExtensionPointRegistryStore extensionPointRegistryStore,
        IHostApplicationEventPublisher eventPublisher,
        ILocalPluginInstallSourceResolver? localPluginInstallSourceResolver = null)
        : this(
            lifecycle,
            activationPolicy,
            entitlementStore,
            auditStore,
            installationRepository,
            unitOfWork,
            packageRegistryReader,
            packageSignatureVerifier,
            nuGetAssemblyResolver,
            extensionPointRegistryStore,
            new EmptyPluginExtensionRegistrationStore(),
            eventPublisher,
            new EmptyWorkspaceManagementStore(),
            localPluginInstallSourceResolver: localPluginInstallSourceResolver)
    {
    }

    public IReadOnlyCollection<HostPluginDescriptor> Plugins => lifecycle.Plugins;

    public async Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(x => new PluginInstallationSnapshot(
                x.PluginId,
                x.DisplayName,
                x.AssemblyPath,
                x.EntryTypeName,
                (int)x.State,
                x.InstalledAtUtc,
                x.UpdatedAtUtc))
            .ToArray();
    }

    public async Task<PluginLifecycleServiceResult> InstallAsync(
        InstallPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInstrumentedLifecycleAsync(
                action: "install",
                pluginId: null,
                requestedBy: command.RequestedBy,
                workspaceKey: null,
                executeAsync: token => InstallFromResolvedAssemblyAsync(
                    assemblyPath: command.AssemblyPath,
                    requestedEntryTypeName: command.EntryTypeName,
                    requestedBy: command.RequestedBy,
                    sourceMetadata: null,
                    cancellationToken: token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInstrumentedLifecycleAsync(
                action: "install",
                pluginId: null,
                requestedBy: command.RequestedBy,
                workspaceKey: null,
                executeAsync: token => InstallFromNuGetCoreAsync(command, token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> InstallFromNuGetCoreAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken)
    {
        var resolved = await nuGetAssemblyResolver
            .ResolveAsync(command.PackageId, command.PackageVersion, command.AssemblyFileName, cancellationToken)
            .ConfigureAwait(false);
        if (!resolved.IsSuccess || string.IsNullOrWhiteSpace(resolved.AssemblyPath))
        {
            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                resolved.Message ?? "NuGet package resolve failed.");
        }

        var metadata = new Dictionary<string, string>
        {
            ["packageId"] = command.PackageId,
            ["packageVersion"] = command.PackageVersion,
            ["assemblyFileName"] = command.AssemblyFileName ?? string.Empty
        };

        return await InstallFromResolvedAssemblyAsync(
                assemblyPath: resolved.AssemblyPath,
                requestedEntryTypeName: command.EntryTypeName,
                requestedBy: command.RequestedBy,
                sourceMetadata: metadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PluginLifecycleServiceResult> UpdateFromNuGetAsync(
        UpdateNuGetPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInstrumentedLifecycleAsync(
                action: "update",
                pluginId: command.PluginId,
                requestedBy: command.RequestedBy,
                workspaceKey: null,
                executeAsync: token => UpdateFromNuGetCoreAsync(command, token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PluginLifecycleServiceResult> UpdateFromLocalAsync(
        UpdateLocalPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInstrumentedLifecycleAsync(
                action: "update",
                pluginId: command.PluginId,
                requestedBy: command.RequestedBy,
                workspaceKey: null,
                executeAsync: token => UpdateFromLocalCoreAsync(command, token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> UpdateFromNuGetCoreAsync(
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

            await WritePluginAuditAsync(
                    action: "plugin.update",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: resolveMessage,
                    metadata: failureMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.update",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: resolveMessage,
                    metadata: null,
                    cancellationToken: cancellationToken)
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

        return await UpdateFromResolvedAssemblyCoreAsync(
                pluginId: command.PluginId,
                assemblyPath: resolved.AssemblyPath,
                requestedEntryTypeName: command.EntryTypeName,
                requestedBy: command.RequestedBy,
                updateMetadata: metadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> UpdateFromLocalCoreAsync(
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

            await WritePluginAuditAsync(
                    action: "plugin.update",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: resolveMessage,
                    metadata: failureMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.update",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: resolveMessage,
                    metadata: null,
                    cancellationToken: cancellationToken)
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

        return await UpdateFromResolvedAssemblyCoreAsync(
                pluginId: command.PluginId,
                assemblyPath: resolved.AssemblyPath,
                requestedEntryTypeName: resolved.EntryTypeName,
                requestedBy: command.RequestedBy,
                updateMetadata: metadata,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> UpdateFromResolvedAssemblyCoreAsync(
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

            await WritePluginAuditAsync(
                    action: "plugin.update",
                    pluginId: pluginId,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: missingMessage,
                    metadata: missingMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.update",
                    pluginId: pluginId,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: missingMessage,
                    metadata: missingMetadata,
                    cancellationToken: cancellationToken)
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
                await WritePluginAuditAsync(
                        action: "plugin.update",
                        pluginId: pluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: deactivate.Message,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await PublishLifecycleEventAsync(
                        action: "plugin.update",
                        pluginId: pluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: deactivate.Message,
                        metadata: null,
                        cancellationToken: cancellationToken)
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

            await WritePluginAuditAsync(
                    action: "plugin.update",
                    pluginId: pluginId,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: uninstall.Message,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.update",
                    pluginId: pluginId,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: uninstall.Message,
                    metadata: null,
                    cancellationToken: cancellationToken)
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

        var install = await InstallFromResolvedAssemblyAsync(
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
                    return await ExecuteRollbackAfterFailedUpdateAsync(
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

                await UpsertInstallationAsync(
                        pluginId: pluginId,
                        displayName: previousDisplayName,
                        assemblyPath: assemblyPath,
                        entryTypeName: requestedEntryTypeName ?? previousEntryTypeName,
                        mark: static (x, now) => x.MarkActivated(now),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            await WritePluginAuditAsync(
                    action: "plugin.update",
                    pluginId: pluginId,
                    isSuccess: true,
                    requestedBy: requestedBy,
                    message: install.Message,
                    metadata: effectiveMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.update",
                    pluginId: pluginId,
                    isSuccess: true,
                    requestedBy: requestedBy,
                    message: install.Message,
                    metadata: effectiveMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Ok,
                true,
                pluginId,
                install.Message,
                WarningMessage: install.WarningMessage,
                WarningCode: install.WarningCode);
        }

        return await ExecuteRollbackAfterFailedUpdateAsync(
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

    private async Task<PluginLifecycleServiceResult> InstallFromResolvedAssemblyAsync(
        string assemblyPath,
        string? requestedEntryTypeName,
        string? requestedBy,
        IReadOnlyDictionary<string, string>? sourceMetadata,
        CancellationToken cancellationToken)
    {
        var packageRead = await packageRegistryReader
            .ReadForAssemblyAsync(assemblyPath, cancellationToken)
            .ConfigureAwait(false);
        if (packageRead.HasRegistryFile && !packageRead.IsValid)
        {
            var reasonCode = MapPackageErrorCode(packageRead.ErrorCode) ?? PluginLifecycleErrorCodes.PluginRegistryInvalid;
            await PublishInstallGateRejectAsync(
                    pluginId: null,
                    requestedBy: requestedBy,
                    message: packageRead.ErrorMessage,
                    gateType: "registry.validation",
                    reasonCode: reasonCode,
                    assemblyPath: assemblyPath,
                    additionalMetadata: packageRead.RegistryPath is null
                        ? null
                        : new Dictionary<string, string>
                        {
                            ["registryPath"] = packageRead.RegistryPath
                        },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                packageRead.ErrorMessage,
                reasonCode);
        }

        var package = packageRead.Registry;
        if (package is not null &&
            !string.Equals(Path.GetFileName(assemblyPath), package.AssemblyFileName, StringComparison.Ordinal))
        {
            var mismatchMessage = $"registry.json expects assembly '{package.AssemblyFileName}', but request uses '{Path.GetFileName(assemblyPath)}'.";
            await PublishInstallGateRejectAsync(
                    pluginId: null,
                    requestedBy: requestedBy,
                    message: mismatchMessage,
                    gateType: "registry.assembly_match",
                    reasonCode: PluginLifecycleErrorCodes.PluginAssemblyFileNameMismatch,
                    assemblyPath: assemblyPath,
                    additionalMetadata: new Dictionary<string, string>
                    {
                        ["registryAssemblyFileName"] = package.AssemblyFileName,
                        ["requestedAssemblyFileName"] = Path.GetFileName(assemblyPath)
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                mismatchMessage,
                PluginLifecycleErrorCodes.PluginAssemblyFileNameMismatch);
        }

        var effectiveEntryTypeName = string.IsNullOrWhiteSpace(requestedEntryTypeName)
            ? package?.EntryTypeName
            : requestedEntryTypeName;

        var signatureVerification = await packageSignatureVerifier
            .VerifyAsync(assemblyPath, cancellationToken)
            .ConfigureAwait(false);
        if (!signatureVerification.IsValid)
        {
            var signatureErrorCode = MapSignatureErrorCode(signatureVerification.ErrorCode)
                ?? PluginLifecycleErrorCodes.PluginPackageSignatureInvalid;
            var signatureMetadata = new Dictionary<string, string>
            {
                ["assemblyPath"] = assemblyPath,
                ["signatureErrorCode"] = signatureErrorCode
            };
            if (!string.IsNullOrWhiteSpace(signatureVerification.SignerThumbprint))
            {
                signatureMetadata["signatureSignerThumbprint"] = signatureVerification.SignerThumbprint;
            }

            await PublishInstallGateRejectAsync(
                    pluginId: null,
                    requestedBy: requestedBy,
                    message: signatureVerification.ErrorMessage,
                    gateType: "signature.validation",
                    reasonCode: signatureErrorCode,
                    assemblyPath: assemblyPath,
                    additionalMetadata: signatureMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                null,
                signatureVerification.ErrorMessage,
                signatureErrorCode);
        }

        var result = await lifecycle.InstallAsync(assemblyPath, effectiveEntryTypeName, cancellationToken)
            .ConfigureAwait(false);

        var installMetadata = new Dictionary<string, string>
        {
            ["assemblyPath"] = assemblyPath,
            ["entryTypeName"] = effectiveEntryTypeName ?? string.Empty
        };
        if (sourceMetadata is not null)
        {
            foreach (var (key, value) in sourceMetadata)
                installMetadata[key] = value;
        }
        if (package is not null)
        {
            installMetadata["registryPath"] = packageRead.RegistryPath ?? string.Empty;
            installMetadata["registryPluginId"] = package.PluginId;
            installMetadata["registryVersion"] = package.Version;
            installMetadata["registryName"] = package.Name;
            installMetadata["registryContractVersion"] = package.ContractVersion;
        }
        if (!string.IsNullOrWhiteSpace(packageRead.WarningMessage))
        {
            installMetadata["registryWarning"] = packageRead.WarningMessage;
        }
        if (!string.IsNullOrWhiteSpace(packageRead.WarningCode))
        {
            installMetadata["registryWarningCode"] = packageRead.WarningCode;
        }
        if (!string.IsNullOrWhiteSpace(signatureVerification.SignerThumbprint))
        {
            installMetadata["signatureSignerThumbprint"] = signatureVerification.SignerThumbprint;
        }

        await WritePluginAuditAsync(
                action: "plugin.install",
                pluginId: result.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: requestedBy,
                message: result.Message,
                metadata: installMetadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.install",
                pluginId: result.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: requestedBy,
                message: result.Message,
                metadata: installMetadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess &&
            package is not null &&
            !string.IsNullOrWhiteSpace(result.PluginId) &&
            !string.Equals(result.PluginId, package.PluginId, StringComparison.OrdinalIgnoreCase))
        {
            _ = await lifecycle.UninstallAsync(result.PluginId, cancellationToken).ConfigureAwait(false);
            await extensionRegistrationStore.RemoveAsync(result.PluginId, cancellationToken).ConfigureAwait(false);

            var mismatchMessage = $"registry.json pluginId '{package.PluginId}' does not match runtime pluginId '{result.PluginId}'.";
            await PublishInstallGateRejectAsync(
                    pluginId: result.PluginId,
                    requestedBy: requestedBy,
                    message: mismatchMessage,
                    gateType: "registry.plugin_id_match",
                    reasonCode: PluginLifecycleErrorCodes.PluginRegistryPluginIdMismatch,
                    assemblyPath: assemblyPath,
                    additionalMetadata: new Dictionary<string, string>
                    {
                        ["registryPluginId"] = package.PluginId,
                        ["runtimePluginId"] = result.PluginId
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                result.PluginId,
                mismatchMessage,
                PluginLifecycleErrorCodes.PluginRegistryPluginIdMismatch);
        }

        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.PluginId))
        {
            // Code-first extension wiring is synced on runtime activation.
            await extensionRegistrationStore.RemoveAsync(result.PluginId, cancellationToken).ConfigureAwait(false);

            var descriptor = FindDescriptor(result.PluginId);
            await UpsertInstalledAsync(
                    pluginId: result.PluginId,
                    displayName: descriptor?.DisplayName ?? result.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? assemblyPath,
                    entryTypeName: descriptor?.EntryTypeName ?? effectiveEntryTypeName,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Ok,
                true,
                result.PluginId,
                result.Message,
                WarningMessage: packageRead.WarningMessage,
                WarningCode: MapPackageWarningCode(packageRead.WarningCode))
            : new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                result.PluginId,
                result.Message,
                WarningMessage: packageRead.WarningMessage,
                WarningCode: MapPackageWarningCode(packageRead.WarningCode));
    }

    public async Task<PluginLifecycleServiceResult> ActivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInstrumentedLifecycleAsync(
                action: "activate",
                pluginId: command.PluginId,
                requestedBy: command.RequestedBy,
                workspaceKey: command.WorkspaceKey,
                executeAsync: token => ActivateCoreAsync(command, token),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PluginLifecycleServiceResult> ActivateCoreAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return await SetWorkspaceScopedActivationAsync(
                    command.PluginId,
                    command.WorkspaceKey,
                    isActive: true,
                    command.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var decision = await activationPolicy.EvaluateAsync(command.PluginId, tenantId: null, cancellationToken).ConfigureAwait(false);
        if (!decision.IsAllowed)
        {
            await WritePluginAuditAsync(
                    action: "plugin.activate",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: decision.Reason,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.activate",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: decision.Reason,
                    metadata: null,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Forbidden,
                false,
                command.PluginId,
                decision.Reason);
        }

        var result = await lifecycle.ActivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await WritePluginAuditAsync(
                action: "plugin.activate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.activate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                metadata: null,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var extensionSync = await SyncRuntimeExtensionRegistrationsAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
            if (!extensionSync.IsSuccess)
            {
                _ = await lifecycle.DeactivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
                await extensionRegistrationStore.RemoveAsync(command.PluginId, cancellationToken).ConfigureAwait(false);

                await PublishInstallGateRejectAsync(
                        pluginId: command.PluginId,
                        requestedBy: command.RequestedBy,
                        message: extensionSync.Message,
                        gateType: "runtime.extension_registration",
                        reasonCode: extensionSync.ErrorCode ?? PluginLifecycleErrorCodes.PluginRegistryInvalid,
                        assemblyPath: string.Empty,
                        additionalMetadata: extensionSync.Metadata,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    command.PluginId,
                    extensionSync.Message,
                    extensionSync.ErrorCode);
            }

            var descriptor = FindDescriptor(command.PluginId);
            await UpsertInstallationAsync(
                    pluginId: command.PluginId,
                    displayName: descriptor?.DisplayName ?? command.PluginId,
                    assemblyPath: descriptor?.AssemblyPath ?? string.Empty,
                    entryTypeName: descriptor?.EntryTypeName,
                    mark: static (x, now) => x.MarkActivated(now),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        return result.IsSuccess
            ? new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.Ok, true, command.PluginId, result.Message)
            : new PluginLifecycleServiceResult(PluginLifecycleServiceStatus.BadRequest, false, command.PluginId, result.Message);
    }

}
