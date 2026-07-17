using Callora.Core.Application.Persistence;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Applies workspace-scoped plugin activation changes behind per-workspace locks.
/// </summary>
public sealed class WorkspaceScopedActivationService(
    IPluginInstallationRepository installationRepository,
    IWorkspaceManagementStore workspaceStore,
    Callora.Core.Application.Plugins.IWorkspacePluginActivationStore activationStore,
    PluginLifecycleReporter reporter,
    WorkspaceLifecycleLockRegistry lockRegistry,
    PluginCapabilityGuard capabilityGuard)
{
    /// <summary>
    /// Activates or deactivates one plugin for one workspace.
    /// </summary>
    public async Task<PluginLifecycleServiceResult> SetActivationAsync(
        string pluginId,
        string workspaceKey,
        bool isActive,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        var normalizedPluginId = pluginId.Trim();
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var lockKey = WorkspaceLifecycleLockRegistry.BuildKey(normalizedPluginId, normalizedWorkspaceKey);
        var workspaceLock = await lockRegistry.AcquireAsync(lockKey, cancellationToken).ConfigureAwait(false);
        try
        {
            var action = isActive ? PluginLifecycleActions.Activate : PluginLifecycleActions.Deactivate;
            var installation = await installationRepository
                .GetByPluginIdAsync(normalizedPluginId, cancellationToken)
                .ConfigureAwait(false);
            if (installation is null || installation.State == PluginInstallationState.Uninstalled)
            {
                var message = $"Plugin '{normalizedPluginId}' is not installed and cannot be scoped to workspace '{normalizedWorkspaceKey}'.";
                await reporter.ReportAsync(
                        new PluginLifecycleReport(
                            Action: action,
                            PluginId: normalizedPluginId,
                            IsSuccess: false,
                            RequestedBy: requestedBy,
                            Message: message,
                            Metadata: new Dictionary<string, string>
                            {
                                ["workspaceKey"] = normalizedWorkspaceKey,
                                ["scope"] = "workspace"
                            }),
                        cancellationToken)
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
                await reporter.WriteAuditAsync(
                        new PluginLifecycleReport(
                            Action: action,
                            PluginId: normalizedPluginId,
                            IsSuccess: false,
                            RequestedBy: requestedBy,
                            Message: message,
                            Metadata: new Dictionary<string, string>
                            {
                                ["workspaceKey"] = normalizedWorkspaceKey,
                                ["scope"] = "workspace"
                            }),
                        cancellationToken)
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
                await reporter.WriteAuditAsync(
                        new PluginLifecycleReport(
                            Action: action,
                            PluginId: normalizedPluginId,
                            IsSuccess: false,
                            RequestedBy: requestedBy,
                            Message: message,
                            Metadata: new Dictionary<string, string>
                            {
                                ["workspaceKey"] = normalizedWorkspaceKey,
                                ["tenantKey"] = workspace.TenantKey,
                                ["scope"] = "workspace"
                            }),
                        cancellationToken)
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
                await reporter.WriteAuditAsync(
                        new PluginLifecycleReport(
                            Action: action,
                            PluginId: normalizedPluginId,
                            IsSuccess: false,
                            RequestedBy: requestedBy,
                            Message: message,
                            Metadata: new Dictionary<string, string>
                            {
                                ["workspaceKey"] = normalizedWorkspaceKey,
                                ["tenantKey"] = workspace.TenantKey,
                                ["scope"] = "workspace"
                            }),
                        cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.Forbidden,
                    false,
                    normalizedPluginId,
                    message);
            }

            var capabilityCheck = isActive
                ? await capabilityGuard.CheckActivationAsync(normalizedPluginId, normalizedWorkspaceKey, cancellationToken).ConfigureAwait(false)
                : await capabilityGuard.CheckDeactivationAsync(normalizedPluginId, normalizedWorkspaceKey, cancellationToken).ConfigureAwait(false);
            if (!capabilityCheck.IsAllowed)
            {
                var capabilityMetadata = new Dictionary<string, string>
                {
                    ["workspaceKey"] = normalizedWorkspaceKey,
                    ["tenantKey"] = workspace.TenantKey,
                    ["scope"] = "workspace"
                };
                if (capabilityCheck.Metadata is not null)
                {
                    foreach (var (key, value) in capabilityCheck.Metadata)
                    {
                        capabilityMetadata[key] = value;
                    }
                }

                await reporter.ReportAsync(
                        new PluginLifecycleReport(
                            Action: action,
                            PluginId: normalizedPluginId,
                            IsSuccess: false,
                            RequestedBy: requestedBy,
                            Message: capabilityCheck.Message,
                            Metadata: capabilityMetadata),
                        cancellationToken)
                    .ConfigureAwait(false);

                return new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    normalizedPluginId,
                    capabilityCheck.Message,
                    isActive
                        ? PluginLifecycleErrorCodes.PluginRequiredCapabilityMissing
                        : PluginLifecycleErrorCodes.PluginCapabilityInUse);
            }

            // Aktivierung ist ein eigener Domänenzustand — sie erzeugt kein
            // Entitlement mehr (PLAT-253); "darf?" prüft der CapabilityGuard.
            await activationStore
                .SetActiveAsync(normalizedPluginId, normalizedWorkspaceKey, workspace.TenantKey, isActive, cancellationToken)
                .ConfigureAwait(false);
            await reporter.ReportAsync(
                    new PluginLifecycleReport(
                        Action: action,
                        PluginId: normalizedPluginId,
                        IsSuccess: true,
                        RequestedBy: requestedBy,
                        Message: isActive
                            ? $"Workspace '{normalizedWorkspaceKey}' activation updated."
                            : $"Workspace '{normalizedWorkspaceKey}' deactivation updated.",
                        Metadata: new Dictionary<string, string>
                        {
                            ["workspaceKey"] = normalizedWorkspaceKey,
                            ["tenantKey"] = workspace.TenantKey,
                            ["scope"] = "workspace"
                        }),
                    cancellationToken)
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
            lockRegistry.Release(lockKey, workspaceLock);
        }
    }
}
