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
    PluginCapabilityGuard capabilityGuard,
    // Optional: Ein Host ohne Fehlerbudget wertet den Faktor als erfüllt. Das hält minimale
    // Kompositionen lauffähig und macht das Budget zu einer Ergänzung, nicht zu einer
    // Voraussetzung der Verfügbarkeitsableitung.
    PluginFaultRegistry? faultRegistry = null) : IPluginAvailabilityEvaluator
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
            RequiredCapabilitiesAvailable: capability.IsAllowed,
            WithinFaultBudget: faultRegistry?.IsWithinBudget(pluginId) ?? true));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, PluginAvailability>> EvaluateManyAsync(
        IReadOnlyCollection<string> pluginIds,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginIds);
        if (pluginIds.Count == 0)
        {
            return new Dictionary<string, PluginAvailability>(StringComparer.OrdinalIgnoreCase);
        }

        // Alles, was am WORKSPACE hängt und nicht am einzelnen Plugin: einmal.
        //
        // Genau hier lag der Aufwand. EvaluateAsync in einer Schleife holte den Workspace, seine
        // aktivierten Plugins und — über den Capability-Guard — die vollständige
        // Installationsliste für JEDES Plugin erneut. Bei zehn aktiven Plugins ist das dieselbe
        // Tabelle zehnmal, während ein Besucher auf eine Seite wartet.
        var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var tenantActive = workspace?.TenantIsActive == true;
        var workspaceActive = workspace?.IsActive == true;

        var activePluginIds = (await activationReader
                .ListActivePluginIdsAsync(workspaceKey, cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        var installationsByPluginId = installations
            .GroupBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        // Der Laufzeitzustand kommt aus dem Speicher, nicht aus der Datenbank — und darf deshalb
        // auch nicht zwischengespeichert werden: Ein abgestürztes Plugin fällt sofort heraus,
        // ohne dass irgendjemand etwas schreibt.
        var healthyPluginIds = lifecycle.Plugins
            .Where(descriptor => descriptor.State is not (HostPluginState.Faulted or HostPluginState.UnloadFailed))
            .Select(descriptor => descriptor.PluginId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, PluginAvailability>(StringComparer.OrdinalIgnoreCase);
        foreach (var pluginId in pluginIds)
        {
            if (result.ContainsKey(pluginId))
            {
                continue;
            }

            var installation = installationsByPluginId.GetValueOrDefault(pluginId);

            // Bleibt je Plugin: Ein Anspruch kann pro Plugin und Mandant unterschiedlich sein,
            // und der Store hat keine Sammelabfrage dafür. Er ist damit der letzte Posten, der
            // mit der Zahl der Plugins wächst.
            var entitled = await entitlementStore
                .IsEntitledAsync(pluginId, workspaceKey, workspace?.TenantKey, cancellationToken)
                .ConfigureAwait(false);

            var capability = installation is null
                ? CapabilityCheckResult.Allowed
                : capabilityGuard.CheckActivation(installation, installations, workspaceKey, activePluginIds);

            result[pluginId] = PluginAvailability.From(new PluginAvailabilityInputs(
                BundledOrInstalled: installation is not null &&
                    installation.State != PluginInstallationState.Uninstalled,
                RuntimeHealthy: healthyPluginIds.Contains(pluginId),
                Entitled: entitled,
                WorkspaceEnabled: activePluginIds.Contains(pluginId),
                TenantActive: tenantActive,
                WorkspaceActive: workspaceActive,
                RequiredCapabilitiesAvailable: capability.IsAllowed,
                WithinFaultBudget: faultRegistry?.IsWithinBudget(pluginId) ?? true));
        }

        return result;
    }
}
