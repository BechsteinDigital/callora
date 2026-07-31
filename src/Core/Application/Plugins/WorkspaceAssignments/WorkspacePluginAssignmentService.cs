using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Plugins;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Application.Plugins.WorkspaceAssignments;

/// <summary>
/// Product-level workspace assignment use case. An assignment is effective only
/// when both independent domain decisions are true: the workspace is entitled
/// to use the plugin and the plugin is activated for that workspace.
/// </summary>
public sealed class WorkspacePluginAssignmentService(
    IWorkspaceManagementStore workspaceStore,
    IPluginLifecycleService lifecycleService,
    IWorkspacePluginActivationReader activationReader,
    IPluginEntitlementStore entitlementStore,
    ILogger<WorkspacePluginAssignmentService> logger)
{
    public async Task<WorkspacePluginAssignmentListResult> ListAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var workspace = await workspaceStore
            .GetAsync(normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return new WorkspacePluginAssignmentListResult(
                WorkspacePluginAssignmentStatus.WorkspaceNotFound,
                [],
                $"Workspace '{normalizedWorkspaceKey}' does not exist.");
        }

        var installations = await lifecycleService
            .GetInstallationsAsync(cancellationToken)
            .ConfigureAwait(false);
        var activePluginIds = await activationReader
            .ListActivePluginIdsAsync(normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        var active = new HashSet<string>(activePluginIds, StringComparer.OrdinalIgnoreCase);
        var items = new List<WorkspacePluginAssignment>();

        foreach (var installation in installations
                     .Where(item => item.State != (int)PluginInstallationState.Uninstalled)
                     .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.PluginId, StringComparer.OrdinalIgnoreCase))
        {
            var isEntitled = await entitlementStore
                .IsEntitledAsync(
                    installation.PluginId,
                    normalizedWorkspaceKey,
                    workspace.TenantKey,
                    cancellationToken)
                .ConfigureAwait(false);
            var isActive = active.Contains(installation.PluginId);
            items.Add(ToAssignment(installation, isEntitled, isActive));
        }

        return new WorkspacePluginAssignmentListResult(
            WorkspacePluginAssignmentStatus.Ok,
            items);
    }

    public async Task<WorkspacePluginAssignmentChangeResult> SetAssignedAsync(
        string workspaceKey,
        string pluginId,
        bool isAssigned,
        string? requestedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedPluginId = pluginId.Trim();

        var workspace = await workspaceStore
            .GetAsync(normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return new WorkspacePluginAssignmentChangeResult(
                WorkspacePluginAssignmentStatus.WorkspaceNotFound,
                message: $"Workspace '{normalizedWorkspaceKey}' does not exist.");
        }

        var installations = await lifecycleService
            .GetInstallationsAsync(cancellationToken)
            .ConfigureAwait(false);
        var installation = installations.FirstOrDefault(
            item =>
                item.State != (int)PluginInstallationState.Uninstalled &&
                string.Equals(item.PluginId, normalizedPluginId, StringComparison.OrdinalIgnoreCase));
        if (installation is null)
        {
            return new WorkspacePluginAssignmentChangeResult(
                WorkspacePluginAssignmentStatus.PluginNotFound,
                message: $"Plugin '{normalizedPluginId}' is not installed.");
        }

        if (isAssigned && installation.State != (int)PluginInstallationState.Active)
        {
            return new WorkspacePluginAssignmentChangeResult(
                WorkspacePluginAssignmentStatus.PluginInactive,
                ToAssignment(installation, isEntitled: false, isActive: false),
                $"Plugin '{normalizedPluginId}' must be globally active before it can be assigned.");
        }

        var activePluginIds = await activationReader
            .ListActivePluginIdsAsync(normalizedWorkspaceKey, cancellationToken)
            .ConfigureAwait(false);
        var wasActive = activePluginIds.Contains(normalizedPluginId, StringComparer.OrdinalIgnoreCase);
        var wasEntitled = await entitlementStore
            .IsEntitledAsync(
                normalizedPluginId,
                normalizedWorkspaceKey,
                workspace.TenantKey,
                cancellationToken)
            .ConfigureAwait(false);

        return isAssigned
            ? await AssignAsync(
                    installation,
                    workspace.TenantKey,
                    normalizedWorkspaceKey,
                    wasActive,
                    wasEntitled,
                    requestedBy,
                    cancellationToken)
                .ConfigureAwait(false)
            : await UnassignAsync(
                    installation,
                    workspace.TenantKey,
                    normalizedWorkspaceKey,
                    wasActive,
                    wasEntitled,
                    requestedBy,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<WorkspacePluginAssignmentChangeResult> AssignAsync(
        PluginInstallationSnapshot installation,
        string tenantKey,
        string workspaceKey,
        bool wasActive,
        bool wasEntitled,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        if (!wasActive)
        {
            var activation = await lifecycleService
                .ActivateAsync(
                    new PluginLifecycleCommand(installation.PluginId, requestedBy, workspaceKey),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!activation.IsSuccess)
            {
                return LifecycleFailure(activation);
            }
        }

        if (!wasEntitled)
        {
            try
            {
                await entitlementStore
                    .SetEntitledAsync(
                        installation.PluginId,
                        true,
                        workspaceKey,
                        tenantKey,
                        source: "workspace-assignment",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to grant plugin {PluginId} to workspace {WorkspaceKey}; rolling activation back.",
                    installation.PluginId,
                    workspaceKey);
                if (!wasActive)
                {
                    _ = await lifecycleService
                        .DeactivateAsync(
                            new PluginLifecycleCommand(installation.PluginId, requestedBy, workspaceKey),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return new WorkspacePluginAssignmentChangeResult(
                    WorkspacePluginAssignmentStatus.PersistenceFailed,
                    message: "The workspace entitlement could not be persisted.");
            }
        }

        return new WorkspacePluginAssignmentChangeResult(
            WorkspacePluginAssignmentStatus.Ok,
            ToAssignment(installation, isEntitled: true, isActive: true));
    }

    private async Task<WorkspacePluginAssignmentChangeResult> UnassignAsync(
        PluginInstallationSnapshot installation,
        string tenantKey,
        string workspaceKey,
        bool wasActive,
        bool wasEntitled,
        string? requestedBy,
        CancellationToken cancellationToken)
    {
        if (wasActive)
        {
            var deactivation = await lifecycleService
                .DeactivateAsync(
                    new PluginLifecycleCommand(installation.PluginId, requestedBy, workspaceKey),
                    cancellationToken)
                .ConfigureAwait(false);
            if (!deactivation.IsSuccess)
            {
                return LifecycleFailure(deactivation);
            }
        }

        if (wasEntitled)
        {
            try
            {
                await entitlementStore
                    .SetEntitledAsync(
                        installation.PluginId,
                        false,
                        workspaceKey,
                        tenantKey,
                        source: "workspace-assignment",
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to revoke plugin {PluginId} from workspace {WorkspaceKey}; rolling activation back.",
                    installation.PluginId,
                    workspaceKey);
                if (wasActive)
                {
                    _ = await lifecycleService
                        .ActivateAsync(
                            new PluginLifecycleCommand(installation.PluginId, requestedBy, workspaceKey),
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                return new WorkspacePluginAssignmentChangeResult(
                    WorkspacePluginAssignmentStatus.PersistenceFailed,
                    message: "The workspace entitlement could not be persisted.");
            }
        }

        return new WorkspacePluginAssignmentChangeResult(
            WorkspacePluginAssignmentStatus.Ok,
            ToAssignment(installation, isEntitled: false, isActive: false));
    }

    private static WorkspacePluginAssignmentChangeResult LifecycleFailure(
        PluginLifecycleServiceResult result) =>
        new(
            result.Status == PluginLifecycleServiceStatus.Forbidden
                ? WorkspacePluginAssignmentStatus.Forbidden
                : WorkspacePluginAssignmentStatus.LifecycleRejected,
            message: result.Message ?? "The workspace plugin lifecycle change was rejected.",
            errorCode: result.ErrorCode);

    private static WorkspacePluginAssignment ToAssignment(
        PluginInstallationSnapshot installation,
        bool isEntitled,
        bool isActive) =>
        new(
            installation.PluginId,
            installation.DisplayName,
            installation.State == (int)PluginInstallationState.Active,
            isEntitled,
            isActive,
            isEntitled && isActive);
}
