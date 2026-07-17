using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// The single place that derives whether a plugin is effectively available in a
/// workspace (REV2 §3.2). Gathers each factor from its owning store and combines
/// them via <see cref="PluginAvailability.From"/>; runtime consumers ask this
/// instead of re-checking entitlement or activation on their own.
/// </summary>
public sealed class PluginAvailabilityEvaluator(
    IPluginInstallationRepository installationRepository,
    IHostPluginLifecycle lifecycle,
    IPluginEntitlementStore entitlementStore,
    IWorkspacePluginActivationReader activationReader,
    IWorkspaceManagementStore workspaceStore,
    PluginCapabilityGuard capabilityGuard) : IPluginAvailabilityEvaluator
{
    public async Task<PluginAvailability> EvaluateAsync(
        string pluginId,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        var installation = await installationRepository
            .GetByPluginIdAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        var bundledOrInstalled = installation is not null &&
            installation.State != PluginInstallationState.Uninstalled;

        // Runtime health is global: the host knows the plugin and it is not in a
        // failure state (Faulted/UnloadFailed, PLAT-255). An unknown plugin is
        // not runtime-healthy.
        var runtimeHealthy = lifecycle.Plugins.Any(descriptor =>
            string.Equals(descriptor.PluginId, pluginId, StringComparison.OrdinalIgnoreCase) &&
            descriptor.State is not (HostPluginState.Faulted or HostPluginState.UnloadFailed));

        var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var tenantActive = workspace?.TenantIsActive == true;
        var workspaceActive = workspace?.IsActive == true;

        var entitled = await entitlementStore
            .IsEntitledAsync(pluginId, workspaceKey, workspace?.TenantKey, cancellationToken)
            .ConfigureAwait(false);

        var activePluginIds = await activationReader
            .ListActivePluginIdsAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);
        var workspaceEnabled = activePluginIds.Contains(pluginId, StringComparer.OrdinalIgnoreCase);

        var capability = await capabilityGuard
            .CheckActivationAsync(pluginId, workspaceKey, cancellationToken)
            .ConfigureAwait(false);

        return PluginAvailability.From(new PluginAvailabilityInputs(
            BundledOrInstalled: bundledOrInstalled,
            RuntimeHealthy: runtimeHealthy,
            Entitled: entitled,
            WorkspaceEnabled: workspaceEnabled,
            TenantActive: tenantActive,
            WorkspaceActive: workspaceActive,
            RequiredCapabilitiesAvailable: capability.IsAllowed));
    }
}
