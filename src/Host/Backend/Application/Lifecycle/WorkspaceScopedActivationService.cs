using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Abstractions.Workspaces;
using Callora.Host.Backend.Domain.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

/// <summary>
/// Applies workspace-scoped plugin activation changes behind per-workspace locks.
/// </summary>
public sealed class WorkspaceScopedActivationService(
    IPluginInstallationRepository installationRepository,
    IWorkspaceManagementStore workspaceStore,
    IPluginEntitlementStore entitlementStore,
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
            var action = isActive ? "plugin.activate" : "plugin.deactivate";
            var installation = await installationRepository
                .GetByPluginIdAsync(normalizedPluginId, cancellationToken)
                .ConfigureAwait(false);
            if (installation is null || installation.State == PluginInstallationState.Uninstalled)
            {
                var message = $"Plugin '{normalizedPluginId}' is not installed and cannot be scoped to workspace '{normalizedWorkspaceKey}'.";
                await reporter.ReportAsync(
                        action: action,
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
                await reporter.WriteAuditAsync(
                        action: action,
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
                await reporter.WriteAuditAsync(
                        action: action,
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
                await reporter.WriteAuditAsync(
                        action: action,
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

            var capabilityCheck = isActive
                ? await capabilityGuard.CheckActivationAsync(normalizedPluginId, normalizedWorkspaceKey, cancellationToken, workspace.TenantKey).ConfigureAwait(false)
                : await capabilityGuard.CheckDeactivationAsync(normalizedPluginId, normalizedWorkspaceKey, cancellationToken, workspace.TenantKey).ConfigureAwait(false);
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
                        action: action,
                        pluginId: normalizedPluginId,
                        isSuccess: false,
                        requestedBy: requestedBy,
                        message: capabilityCheck.Message,
                        metadata: capabilityMetadata,
                        cancellationToken: cancellationToken)
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

            await entitlementStore
                .SetEntitledAsync(normalizedPluginId, isActive, normalizedWorkspaceKey, workspace.TenantKey, cancellationToken)
                .ConfigureAwait(false);
            await reporter.ReportAsync(
                    action: action,
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
            lockRegistry.Release(lockKey, workspaceLock);
        }
    }
}
