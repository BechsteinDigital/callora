using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Audit;
using Callora.Core.Application.Events;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Workspaces;
using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Hosting.Application.Plugins;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Facade for all plugin lifecycle operations, composed from focused collaborators.
/// </summary>
public sealed class PluginLifecycleService : IPluginLifecycleService
{
    private readonly IHostPluginLifecycle _lifecycle;
    private readonly IPluginActivationPolicy _activationPolicy;
    private readonly IPluginEntitlementStore _entitlementStore;
    private readonly IPluginInstallationRepository _installationRepository;
    private readonly IPluginExtensionRegistrationStore _extensionRegistrationStore;
    private readonly PluginLifecycleReporter _reporter;
    private readonly PluginInstallationRecorder _recorder;
    private readonly PluginExtensionSynchronizer _extensionSynchronizer;
    private readonly PluginInstaller _installer;
    private readonly PluginUpdater _updater;
    private readonly WorkspaceScopedActivationService _workspaceActivation;
    private readonly WorkspaceLifecycleLockRegistry _workspaceLifecycleLocks;
    private readonly PluginCapabilityGuard _capabilityGuard;

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
        IPluginExtensionRegistrationStore extensionRegistrationStore,
        IHostApplicationEventPublisher eventPublisher,
        IWorkspaceManagementStore workspaceStore,
        ICalloraPluginCatalog? pluginCatalog = null,
        ILocalPluginInstallSourceResolver? localPluginInstallSourceResolver = null,
        Callora.Core.Application.Plugins.IWorkspacePluginActivationStore? workspaceActivationStore = null,
        Callora.Core.Application.Plugins.IWorkspacePluginActivationReader? workspaceActivationReader = null)
    {
        _lifecycle = lifecycle;
        _activationPolicy = activationPolicy;
        _entitlementStore = entitlementStore;
        _installationRepository = installationRepository;
        _extensionRegistrationStore = extensionRegistrationStore;

        var catalog = pluginCatalog ?? EmptyCalloraPluginCatalog.Instance;
        _reporter = new PluginLifecycleReporter(auditStore, eventPublisher);
        _recorder = new PluginInstallationRecorder(installationRepository, unitOfWork);
        _extensionSynchronizer = new PluginExtensionSynchronizer(
            catalog,
            extensionPointRegistryStore,
            extensionRegistrationStore);
        _installer = new PluginInstaller(
            lifecycle,
            packageRegistryReader,
            packageSignatureVerifier,
            nuGetAssemblyResolver,
            extensionRegistrationStore,
            _reporter,
            _recorder);
        _updater = new PluginUpdater(
            lifecycle,
            nuGetAssemblyResolver,
            localPluginInstallSourceResolver,
            installationRepository,
            _installer,
            _recorder,
            _reporter,
            _extensionSynchronizer);
        var activationStore = workspaceActivationStore
            ?? new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        // The guard must read the same activations the service writes; in-memory hosts share one
        // instance, EF hosts get store + reader over the same scoped DbContext (PLAT-253).
        var activationReader = workspaceActivationReader
            ?? activationStore as Callora.Core.Application.Plugins.IWorkspacePluginActivationReader
            ?? new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        _capabilityGuard = new PluginCapabilityGuard(installationRepository, activationReader);
        _workspaceLifecycleLocks = new WorkspaceLifecycleLockRegistry();
        _workspaceActivation = new WorkspaceScopedActivationService(
            installationRepository,
            workspaceStore,
            activationStore,
            _reporter,
            _workspaceLifecycleLocks,
            _capabilityGuard);
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
        IHostApplicationEventPublisher eventPublisher,
        ILocalPluginInstallSourceResolver? localPluginInstallSourceResolver = null,
        Callora.Core.Application.Plugins.IWorkspacePluginActivationStore? workspaceActivationStore = null)
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
            localPluginInstallSourceResolver: localPluginInstallSourceResolver,
            workspaceActivationStore: workspaceActivationStore)
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

    public IReadOnlyCollection<HostPluginDescriptor> Plugins => _lifecycle.Plugins;

    public async Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
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

    public Task<PluginLifecycleServiceResult> InstallAsync(
        InstallPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return PluginLifecycleInstrumentation.ExecuteAsync(
            action: "install",
            pluginId: null,
            requestedBy: command.RequestedBy,
            workspaceKey: null,
            executeAsync: token => _installer.InstallFromResolvedAssemblyAsync(
                assemblyPath: command.AssemblyPath,
                requestedEntryTypeName: command.EntryTypeName,
                requestedBy: command.RequestedBy,
                sourceMetadata: null,
                cancellationToken: token),
            cancellationToken: cancellationToken);
    }

    public Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(
        InstallNuGetPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return PluginLifecycleInstrumentation.ExecuteAsync(
            action: "install",
            pluginId: null,
            requestedBy: command.RequestedBy,
            workspaceKey: null,
            executeAsync: token => _installer.InstallFromNuGetAsync(command, token),
            cancellationToken: cancellationToken);
    }

    public Task<PluginLifecycleServiceResult> UpdateFromNuGetAsync(
        UpdateNuGetPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return PluginLifecycleInstrumentation.ExecuteAsync(
            action: "update",
            pluginId: command.PluginId,
            requestedBy: command.RequestedBy,
            workspaceKey: null,
            executeAsync: token => _updater.UpdateFromNuGetAsync(command, token),
            cancellationToken: cancellationToken);
    }

    public Task<PluginLifecycleServiceResult> UpdateFromLocalAsync(
        UpdateLocalPluginCommand command,
        CancellationToken cancellationToken = default)
    {
        return PluginLifecycleInstrumentation.ExecuteAsync(
            action: "update",
            pluginId: command.PluginId,
            requestedBy: command.RequestedBy,
            workspaceKey: null,
            executeAsync: token => _updater.UpdateFromLocalAsync(command, token),
            cancellationToken: cancellationToken);
    }

    public Task<PluginLifecycleServiceResult> ActivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        return PluginLifecycleInstrumentation.ExecuteAsync(
            action: "activate",
            pluginId: command.PluginId,
            requestedBy: command.RequestedBy,
            workspaceKey: command.WorkspaceKey,
            executeAsync: token => ActivateCoreAsync(command, token),
            cancellationToken: cancellationToken);
    }

    public Task<PluginLifecycleServiceResult> DeactivateAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        return PluginLifecycleInstrumentation.ExecuteAsync(
            action: "deactivate",
            pluginId: command.PluginId,
            requestedBy: command.RequestedBy,
            workspaceKey: command.WorkspaceKey,
            executeAsync: token => DeactivateCoreAsync(command, token),
            cancellationToken: cancellationToken);
    }

    public Task<PluginLifecycleServiceResult> UninstallAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken = default)
    {
        return PluginLifecycleInstrumentation.ExecuteAsync(
            action: "uninstall",
            pluginId: command.PluginId,
            requestedBy: command.RequestedBy,
            workspaceKey: command.WorkspaceKey,
            executeAsync: token => UninstallCoreAsync(command, token),
            cancellationToken: cancellationToken);
    }

    private async Task<PluginLifecycleServiceResult> ActivateCoreAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return await _workspaceActivation.SetActivationAsync(
                    command.PluginId,
                    command.WorkspaceKey,
                    isActive: true,
                    command.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var decision = await _activationPolicy.EvaluateAsync(command.PluginId, tenantId: null, cancellationToken).ConfigureAwait(false);
        if (!decision.IsAllowed)
        {
            await _reporter.ReportAsync(
                    action: "plugin.activate",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: decision.Reason,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Forbidden,
                false,
                command.PluginId,
                decision.Reason);
        }

        var capabilityCheck = await _capabilityGuard
            .CheckActivationAsync(command.PluginId, workspaceKey: null, cancellationToken)
            .ConfigureAwait(false);
        if (!capabilityCheck.IsAllowed)
        {
            await _reporter.ReportAsync(
                    action: "plugin.activate",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: capabilityCheck.Message,
                    metadata: capabilityCheck.Metadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                command.PluginId,
                capabilityCheck.Message,
                PluginLifecycleErrorCodes.PluginRequiredCapabilityMissing);
        }

        var result = await _lifecycle.ActivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await _reporter.ReportAsync(
                action: "plugin.activate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            var extensionSync = await _extensionSynchronizer.SyncAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
            if (!extensionSync.IsSuccess)
            {
                _ = await _lifecycle.DeactivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
                await _extensionRegistrationStore.RemoveAsync(command.PluginId, cancellationToken).ConfigureAwait(false);

                await _reporter.ReportInstallGateRejectAsync(
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

            var descriptor = _lifecycle.FindDescriptor(command.PluginId);
            await _recorder.MarkAsync(
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

    private async Task<PluginLifecycleServiceResult> DeactivateCoreAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return await _workspaceActivation.SetActivationAsync(
                    command.PluginId,
                    command.WorkspaceKey,
                    isActive: false,
                    command.RequestedBy,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var capabilityCheck = await _capabilityGuard
            .CheckDeactivationAsync(command.PluginId, workspaceKey: null, cancellationToken)
            .ConfigureAwait(false);
        if (!capabilityCheck.IsAllowed)
        {
            await _reporter.ReportAsync(
                    action: "plugin.deactivate",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: capabilityCheck.Message,
                    metadata: capabilityCheck.Metadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                command.PluginId,
                capabilityCheck.Message,
                PluginLifecycleErrorCodes.PluginCapabilityInUse);
        }

        var result = await _lifecycle.DeactivateAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await _reporter.ReportAsync(
                action: "plugin.deactivate",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await _entitlementStore.ClearForPluginAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
            await _extensionRegistrationStore.RemoveAsync(command.PluginId, cancellationToken).ConfigureAwait(false);

            var descriptor = _lifecycle.FindDescriptor(command.PluginId);
            await _recorder.MarkAsync(
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

    private async Task<PluginLifecycleServiceResult> UninstallCoreAsync(
        PluginLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        var capabilityCheck = await _capabilityGuard
            .CheckDeactivationAsync(command.PluginId, workspaceKey: null, cancellationToken)
            .ConfigureAwait(false);
        if (!capabilityCheck.IsAllowed)
        {
            await _reporter.ReportAsync(
                    action: "plugin.uninstall",
                    pluginId: command.PluginId,
                    isSuccess: false,
                    requestedBy: command.RequestedBy,
                    message: capabilityCheck.Message,
                    metadata: capabilityCheck.Metadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.BadRequest,
                false,
                command.PluginId,
                capabilityCheck.Message,
                PluginLifecycleErrorCodes.PluginCapabilityInUse);
        }

        var result = await _lifecycle.UninstallAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
        await _reporter.ReportAsync(
                action: "plugin.uninstall",
                pluginId: command.PluginId,
                isSuccess: result.IsSuccess,
                requestedBy: command.RequestedBy,
                message: result.Message,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.IsSuccess)
        {
            await _entitlementStore.ClearForPluginAsync(command.PluginId, cancellationToken).ConfigureAwait(false);
            await _extensionRegistrationStore.RemoveAsync(command.PluginId, cancellationToken).ConfigureAwait(false);

            var descriptor = _lifecycle.FindDescriptor(command.PluginId);
            await _recorder.MarkAsync(
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
