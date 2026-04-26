using System.Diagnostics;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Audit;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Domain.Extensions;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Modules.Abstractions.Application.Plugins;
using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

public sealed partial class PluginLifecycleService
{
    private async Task<PluginLifecycleServiceResult> ExecuteRollbackAfterFailedUpdateAsync(
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
            ["triggerAction"] = "plugin.update",
            ["triggerMessage"] = rollbackTrigger,
            ["rollbackAssemblyPath"] = previousAssemblyPath
        };

        var rollbackInstall = await lifecycle
            .InstallAsync(previousAssemblyPath, previousEntryTypeName, cancellationToken)
            .ConfigureAwait(false);
        if (!rollbackInstall.IsSuccess)
        {
            await WritePluginAuditAsync(
                    action: "plugin.rollback",
                    pluginId: pluginId,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: rollbackInstall.Message,
                    metadata: rollbackMetadata,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: "plugin.rollback",
                    pluginId: pluginId,
                    isSuccess: false,
                    requestedBy: requestedBy,
                    message: rollbackInstall.Message,
                    metadata: rollbackMetadata,
                    cancellationToken: cancellationToken)
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
                await WritePluginAuditAsync(
                        action: "plugin.rollback",
                        pluginId: pluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: rollbackActivate.Message,
                        metadata: rollbackMetadata,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await PublishLifecycleEventAsync(
                        action: "plugin.rollback",
                        pluginId: pluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: rollbackActivate.Message,
                        metadata: rollbackMetadata,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    pluginId,
                    $"Plugin update failed and rollback activation failed: {rollbackActivate.Message}",
                    PluginLifecycleErrorCodes.PluginRollbackFailed);
            }

            var extensionSync = await SyncRuntimeExtensionRegistrationsAsync(pluginId, cancellationToken).ConfigureAwait(false);
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
        await RestoreInstallationAfterRollbackAsync(
                pluginId,
                previousDisplayName,
                previousAssemblyPath,
                previousEntryTypeName,
                previousState,
                cancellationToken)
            .ConfigureAwait(false);

        await WritePluginAuditAsync(
                action: "plugin.rollback",
                pluginId: pluginId,
                isSuccess: true,
                requestedBy: requestedBy,
                message: "Rollback restored previous stable plugin version.",
                metadata: rollbackMetadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.rollback",
                pluginId: pluginId,
                isSuccess: true,
                requestedBy: requestedBy,
                message: "Rollback restored previous stable plugin version.",
                metadata: rollbackMetadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await WritePluginAuditAsync(
                action: "plugin.update",
                pluginId: pluginId,
                isSuccess: false,
                requestedBy: requestedBy,
                message: rollbackTrigger,
                metadata: new Dictionary<string, string>
                {
                    ["rollbackTriggered"] = "true",
                    ["rollbackAction"] = "plugin.rollback"
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await PublishLifecycleEventAsync(
                action: "plugin.update",
                pluginId: pluginId,
                isSuccess: false,
                requestedBy: requestedBy,
                message: rollbackTrigger,
                metadata: new Dictionary<string, string>
                {
                    ["rollbackTriggered"] = "true",
                    ["rollbackAction"] = "plugin.rollback"
                },
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new PluginLifecycleServiceResult(
            PluginLifecycleServiceStatus.BadRequest,
            false,
            pluginId,
            $"Plugin update failed. Rollback restored previous version: {rollbackTrigger}");
    }

    private async Task<PluginLifecycleServiceResult> SetWorkspaceScopedActivationAsync(
        string pluginId,
        string workspaceKey,
        bool isActive,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        var normalizedPluginId = pluginId.Trim();
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var lockKey = BuildWorkspaceLockKey(normalizedPluginId, normalizedWorkspaceKey);
        var workspaceLock = await AcquireWorkspaceLifecycleLockAsync(lockKey, cancellationToken).ConfigureAwait(false);
        try
        {
            var installation = await installationRepository
                .GetByPluginIdAsync(normalizedPluginId, cancellationToken)
                .ConfigureAwait(false);
            if (installation is null || installation.State == PluginInstallationState.Uninstalled)
            {
                var message = $"Plugin '{normalizedPluginId}' is not installed and cannot be scoped to workspace '{normalizedWorkspaceKey}'.";
                await WritePluginAuditAsync(
                        action: isActive ? "plugin.activate" : "plugin.deactivate",
                        pluginId: normalizedPluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: message,
                        metadata: new Dictionary<string, string>
                        {
                            ["workspaceKey"] = normalizedWorkspaceKey,
                            ["scope"] = "workspace"
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                await PublishLifecycleEventAsync(
                        action: isActive ? "plugin.activate" : "plugin.deactivate",
                        pluginId: normalizedPluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: message,
                        metadata: new Dictionary<string, string>
                        {
                            ["workspaceKey"] = normalizedWorkspaceKey,
                            ["scope"] = "workspace"
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    normalizedPluginId,
                    message);
            }

            var workspace = await workspaceStore
                .GetAsync(normalizedWorkspaceKey, cancellationToken)
                .ConfigureAwait(false);
            if (workspace is null)
            {
                var message = $"Workspace '{normalizedWorkspaceKey}' does not exist.";
                await WritePluginAuditAsync(
                        action: isActive ? "plugin.activate" : "plugin.deactivate",
                        pluginId: normalizedPluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: message,
                        metadata: new Dictionary<string, string>
                        {
                            ["workspaceKey"] = normalizedWorkspaceKey,
                            ["scope"] = "workspace"
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    normalizedPluginId,
                    message);
            }

            if (!workspace.TenantIsActive)
            {
                var message = $"Tenant '{workspace.TenantKey}' is suspended and blocks workspace activation.";
                await WritePluginAuditAsync(
                        action: isActive ? "plugin.activate" : "plugin.deactivate",
                        pluginId: normalizedPluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: message,
                        metadata: new Dictionary<string, string>
                        {
                            ["workspaceKey"] = normalizedWorkspaceKey,
                            ["tenantKey"] = workspace.TenantKey,
                            ["scope"] = "workspace"
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.Forbidden,
                    false,
                    normalizedPluginId,
                    message);
            }

            if (!workspace.IsActive)
            {
                var message = $"Workspace '{normalizedWorkspaceKey}' is inactive and blocks plugin lifecycle updates.";
                await WritePluginAuditAsync(
                        action: isActive ? "plugin.activate" : "plugin.deactivate",
                        pluginId: normalizedPluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: message,
                        metadata: new Dictionary<string, string>
                        {
                            ["workspaceKey"] = normalizedWorkspaceKey,
                            ["tenantKey"] = workspace.TenantKey,
                            ["scope"] = "workspace"
                        },
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.Forbidden,
                    false,
                    normalizedPluginId,
                    message);
            }

            await entitlementStore
                .SetEntitledAsync(normalizedPluginId, isActive, normalizedWorkspaceKey, workspace.TenantKey, cancellationToken)
                .ConfigureAwait(false);
            await WritePluginAuditAsync(
                    action: isActive ? "plugin.activate" : "plugin.deactivate",
                    pluginId: normalizedPluginId,
                    isSuccess: true,
                    requestedBy: requestedBy,
                    message: isActive
                        ? $"Workspace '{normalizedWorkspaceKey}' activation updated."
                        : $"Workspace '{normalizedWorkspaceKey}' deactivation updated.",
                    metadata: new Dictionary<string, string>
                    {
                        ["workspaceKey"] = normalizedWorkspaceKey,
                        ["tenantKey"] = workspace.TenantKey,
                        ["scope"] = "workspace"
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await PublishLifecycleEventAsync(
                    action: isActive ? "plugin.activate" : "plugin.deactivate",
                    pluginId: normalizedPluginId,
                    isSuccess: true,
                    requestedBy: requestedBy,
                    message: isActive
                        ? $"Workspace '{normalizedWorkspaceKey}' activation updated."
                        : $"Workspace '{normalizedWorkspaceKey}' deactivation updated.",
                    metadata: new Dictionary<string, string>
                    {
                        ["workspaceKey"] = normalizedWorkspaceKey,
                        ["tenantKey"] = workspace.TenantKey,
                        ["scope"] = "workspace"
                    },
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Ok,
                true,
                normalizedPluginId,
                isActive
                    ? $"Plugin '{normalizedPluginId}' is active for workspace '{normalizedWorkspaceKey}'."
                    : $"Plugin '{normalizedPluginId}' is inactive for workspace '{normalizedWorkspaceKey}'.");
        }
        finally
        {
            ReleaseWorkspaceLifecycleLock(lockKey, workspaceLock);
        }
    }

    private async Task<(bool IsSuccess, string? Message, string? ErrorCode, IReadOnlyDictionary<string, string>? Metadata)>
        SyncRuntimeExtensionRegistrationsAsync(
            string pluginId,
            CancellationToken cancellationToken)
    {
        var contributors = _pluginCatalog.GetExports<IHostPluginExtensionContributor>()
            .Where(x => string.Equals(x.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (contributors.Length == 0)
        {
            await extensionRegistrationStore.RemoveAsync(pluginId, cancellationToken).ConfigureAwait(false);
            return (true, null, null, null);
        }

        var capabilities = contributors
            .SelectMany(x => x.Capabilities)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var registrations = new List<PluginPackageExtensionRegistration>();
        foreach (var contributor in contributors)
        {
            var contributorRegistrations = contributor.GetRegistrations();
            var validation = await ValidateRuntimeExtensionRegistrationsAsync(
                    contributorRegistrations,
                    capabilities,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return (false, validation.Message, validation.ReasonCode, validation.Metadata);
            }

            foreach (var registration in contributorRegistrations)
            {
                if (!ExtensionSurfaceCodes.TryParse(registration.Surface, out var surface))
                {
                    return (
                        false,
                        $"Runtime extension registration for '{registration.ExtensionPointId}' has invalid surface '{registration.Surface}'.",
                        PluginLifecycleErrorCodes.PluginExtensionSurfaceMismatch,
                        new Dictionary<string, string>
                        {
                            ["extensionPointId"] = registration.ExtensionPointId,
                            ["extensionSurface"] = registration.Surface
                        });
                }

                registrations.Add(new PluginPackageExtensionRegistration(
                    registration.ExtensionPointId.Trim(),
                    surface));
            }
        }

        await extensionRegistrationStore
            .UpsertAsync(
                pluginId,
                registrations,
                capabilities,
                cancellationToken)
            .ConfigureAwait(false);

        return (true, null, null, null);
    }

    private Task RestoreInstallationAfterRollbackAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        PluginInstallationState previousState,
        CancellationToken cancellationToken)
    {
        return previousState switch
        {
            PluginInstallationState.Active => UpsertInstallationAsync(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                static (x, now) => x.MarkActivated(now),
                cancellationToken),
            PluginInstallationState.Inactive => UpsertInstallationAsync(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                static (x, now) => x.MarkDeactivated(now),
                cancellationToken),
            _ => UpsertInstalledAsync(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                cancellationToken)
        };
    }

    private async Task<PluginLifecycleServiceResult> ExecuteInstrumentedLifecycleAsync(
        string action,
        string? pluginId,
        string? requestedBy,
        string? workspaceKey,
        Func<CancellationToken, Task<PluginLifecycleServiceResult>> executeAsync,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var scope = string.IsNullOrWhiteSpace(workspaceKey) ? "global" : "workspace";
        using var activity = PluginLifecycleTelemetry.StartOperationActivity(action, pluginId, requestedBy, workspaceKey, scope);

        PluginLifecycleServiceResult? result = null;
        try
        {
            result = await executeAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception)
        {
            PluginLifecycleTelemetry.RecordException(activity, exception);
            throw;
        }
        finally
        {
            PluginLifecycleTelemetry.CompleteOperation(
                action,
                scope,
                activity,
                result?.IsSuccess ?? false,
                result?.ErrorCode,
                startedAt);
        }
    }

    private static string BuildWorkspaceLockKey(string pluginId, string workspaceKey) =>
        $"{workspaceKey}:{pluginId}";

    private async Task<WorkspaceLifecycleLock> AcquireWorkspaceLifecycleLockAsync(
        string lockKey,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = _workspaceLifecycleLocks.GetOrAdd(lockKey, _ => new WorkspaceLifecycleLock());
            Interlocked.Increment(ref candidate.ReferenceCount);

            if (_workspaceLifecycleLocks.TryGetValue(lockKey, out var current) &&
                ReferenceEquals(candidate, current))
            {
                await candidate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                return candidate;
            }

            if (Interlocked.Decrement(ref candidate.ReferenceCount) == 0)
            {
                _workspaceLifecycleLocks.TryRemove(new KeyValuePair<string, WorkspaceLifecycleLock>(lockKey, candidate));
            }
        }
    }

    private void ReleaseWorkspaceLifecycleLock(string lockKey, WorkspaceLifecycleLock lockState)
    {
        lockState.Semaphore.Release();
        if (Interlocked.Decrement(ref lockState.ReferenceCount) == 0)
        {
            _workspaceLifecycleLocks.TryRemove(new KeyValuePair<string, WorkspaceLifecycleLock>(lockKey, lockState));
        }
    }

    private Task WritePluginAuditAsync(
        string action,
        string? pluginId,
        bool isSuccess,
        string? requestedBy,
        string? message,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveMetadata = EnrichMetadataWithCorrelationId(metadata);
        return auditStore.WritePluginAuditAsync(
            action,
            pluginId,
            isSuccess,
            requestedBy,
            message,
            effectiveMetadata,
            cancellationToken);
    }

    private HostPluginDescriptor? FindDescriptor(string pluginId)
    {
        foreach (var plugin in lifecycle.Plugins)
        {
            if (string.Equals(plugin.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
                return plugin;
        }

        return null;
    }

    private async Task UpsertInstallationAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        Action<PluginInstallation, DateTimeOffset> mark,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var installation = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);

        if (installation is null)
        {
            var safeAssemblyPath = string.IsNullOrWhiteSpace(assemblyPath) ? "unknown" : assemblyPath;
            installation = PluginInstallation.CreateInstalled(
                pluginId,
                displayName,
                safeAssemblyPath,
                entryTypeName,
                now);

            await installationRepository.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        }
        else if (!string.IsNullOrWhiteSpace(assemblyPath))
        {
            installation.ApplyInstallMetadata(displayName, assemblyPath, entryTypeName, now);
        }

        mark(installation, now);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertInstalledAsync(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var installation = await installationRepository.GetByPluginIdAsync(pluginId, cancellationToken).ConfigureAwait(false);

        if (installation is null)
        {
            installation = PluginInstallation.CreateInstalled(
                pluginId,
                displayName,
                assemblyPath,
                entryTypeName,
                now);

            await installationRepository.AddAsync(installation, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            installation.ApplyInstallMetadata(displayName, assemblyPath, entryTypeName, now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task PublishLifecycleEventAsync(
        string action,
        string? pluginId,
        bool isSuccess,
        string? requestedBy,
        string? message,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        var effectiveMetadata = EnrichMetadataWithCorrelationId(metadata);
        return eventPublisher.PublishAsync(
            new PluginLifecycleChangedEvent(
                DateTimeOffset.UtcNow,
                action,
                pluginId,
                isSuccess,
                requestedBy,
                message,
                effectiveMetadata),
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, string>? EnrichMetadataWithCorrelationId(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var correlationId = PluginLifecycleTelemetry.GetCurrentCorrelationId();
        if (string.IsNullOrWhiteSpace(correlationId))
            return metadata;

        var enriched = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        enriched["correlationId"] = correlationId;
        return enriched;
    }

    private static string? MapPackageErrorCode(string? packageErrorCode) =>
        packageErrorCode switch
        {
            PluginRegistryErrorCodes.ContractVersionUnsupported => PluginLifecycleErrorCodes.PluginContractVersionUnsupported,
            PluginRegistryErrorCodes.ContractVersionRemoved => PluginLifecycleErrorCodes.PluginContractVersionRemoved,
            _ => packageErrorCode,
        };

    private static string? MapPackageWarningCode(string? packageWarningCode) =>
        packageWarningCode switch
        {
            PluginRegistryErrorCodes.ContractVersionDeprecated => PluginLifecycleWarningCodes.PluginContractVersionDeprecated,
            _ => packageWarningCode,
        };

    private static string? MapSignatureErrorCode(string? signatureErrorCode) =>
        signatureErrorCode switch
        {
            PluginPackageSignatureErrorCodes.UnsignedPackage => PluginLifecycleErrorCodes.PluginPackageUnsigned,
            PluginPackageSignatureErrorCodes.InvalidSignature => PluginLifecycleErrorCodes.PluginPackageSignatureInvalid,
            PluginPackageSignatureErrorCodes.UntrustedSigner => PluginLifecycleErrorCodes.PluginPackageSignerUntrusted,
            _ => signatureErrorCode,
        };

    private async Task PublishInstallGateRejectAsync(
        string? pluginId,
        string? requestedBy,
        string? message,
        string gateType,
        string reasonCode,
        string assemblyPath,
        IReadOnlyDictionary<string, string>? additionalMetadata,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            ["gateType"] = gateType,
            ["reasonCode"] = reasonCode,
            ["assemblyPath"] = assemblyPath
        };

        if (additionalMetadata is not null)
        {
            foreach (var (key, value) in additionalMetadata)
            {
                metadata[key] = value;
            }
        }

        await WritePluginAuditAsync(
                action: "plugin.install",
                pluginId: pluginId,
                isSuccess: false,
                requestedBy: requestedBy,
                message: message,
                metadata: metadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        await PublishLifecycleEventAsync(
                action: "plugin.install",
                pluginId: pluginId,
                isSuccess: false,
                requestedBy: requestedBy,
                message: message,
                metadata: metadata,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(bool IsValid, string? Message, string? ReasonCode, IReadOnlyDictionary<string, string>? Metadata)>
        ValidateRuntimeExtensionRegistrationsAsync(
            IReadOnlyList<HostPluginExtensionRegistration> extensionRegistrations,
            IReadOnlyList<string> capabilities,
            CancellationToken cancellationToken)
    {
        var normalizedCapabilities = new HashSet<string>(
            capabilities
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var extensionRegistration in extensionRegistrations)
        {
            var extensionPoint = await extensionPointRegistryStore
                .FindByIdAsync(extensionRegistration.ExtensionPointId, cancellationToken)
                .ConfigureAwait(false);
            if (extensionPoint is null)
            {
                return (
                    IsValid: false,
                    Message: $"Runtime extension point '{extensionRegistration.ExtensionPointId}' is not registered.",
                    ReasonCode: PluginLifecycleErrorCodes.PluginExtensionPointUnknown,
                    Metadata: new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["extensionSurface"] = extensionRegistration.Surface
                    });
            }

            if (!ExtensionSurfaceCodes.TryParse(extensionRegistration.Surface, out var surface))
            {
                return (
                    IsValid: false,
                    Message: $"Runtime extension point '{extensionRegistration.ExtensionPointId}' has invalid surface '{extensionRegistration.Surface}'.",
                    ReasonCode: PluginLifecycleErrorCodes.PluginExtensionSurfaceMismatch,
                    Metadata: new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["extensionSurface"] = extensionRegistration.Surface
                    });
            }

            if (extensionPoint.Surface != surface)
            {
                return (
                    IsValid: false,
                    Message:
                    $"Runtime extension point '{extensionRegistration.ExtensionPointId}' is '{extensionPoint.Surface.ToCode()}', but plugin registers '{extensionRegistration.Surface}'.",
                    ReasonCode: PluginLifecycleErrorCodes.PluginExtensionSurfaceMismatch,
                    Metadata: new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["extensionSurface"] = extensionRegistration.Surface,
                        ["expectedSurface"] = extensionPoint.Surface.ToCode()
                    });
            }

            if (!normalizedCapabilities.Contains(extensionPoint.RequiredScope))
            {
                return (
                    IsValid: false,
                    Message:
                    $"Runtime extension point '{extensionRegistration.ExtensionPointId}' requires scope '{extensionPoint.RequiredScope}', but plugin does not declare it.",
                    ReasonCode: PluginLifecycleErrorCodes.PluginExtensionScopeMissing,
                    Metadata: new Dictionary<string, string>
                    {
                        ["extensionPointId"] = extensionRegistration.ExtensionPointId,
                        ["requiredScope"] = extensionPoint.RequiredScope
                    });
            }
        }

        return (IsValid: true, Message: null, ReasonCode: null, Metadata: null);
    }

}
