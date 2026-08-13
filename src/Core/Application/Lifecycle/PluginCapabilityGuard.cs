using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Validates capability dependencies between plugins before activation and deactivation.
/// A plugin declaring <c>requiresCapabilities</c> can only be activated when another
/// active plugin provides each required capability in the same scope.
/// </summary>
public sealed class PluginCapabilityGuard(
    IPluginInstallationRepository installationRepository,
    IWorkspacePluginActivationReader activationReader,
    RuntimeCapabilityRegistry? runtimeCapabilities = null)
{
    /// <summary>
    /// Checks whether all required capabilities of one plugin are provided in the target scope.
    /// Global scope (<paramref name="workspaceKey"/> null) requires globally active providers;
    /// workspace scope requires providers actually activated in the same workspace. Activation —
    /// not entitlement — decides who is running here (PLAT-253).
    /// </summary>
    public async Task<CapabilityCheckResult> CheckActivationAsync(
        string pluginId,
        string? workspaceKey,
        CancellationToken cancellationToken)
    {
        var installation = await installationRepository
            .GetByPluginIdAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (installation is null)
        {
            return CapabilityCheckResult.Allowed;
        }

        var required = installation.GetRequiredCapabilities();
        if (required.Count == 0)
        {
            return CapabilityCheckResult.Allowed;
        }

        var activeInScope = await LoadActiveInScopeAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);

        return CheckActivation(installation, installations, workspaceKey, activeInScope);
    }

    /// <summary>
    /// Dieselbe Prüfung wie <see cref="CheckActivationAsync"/>, aber mit bereits geladenen Daten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Getrennt, weil die Regel eine Sache ist und ihre Beschaffung eine andere. Wer viele Plugins
    /// hintereinander prüft, lädt die Installationsliste sonst je Plugin einmal — bei zehn aktiven
    /// Plugins zehnmal dieselbe Tabelle, und das innerhalb einer Schleife, die schon läuft, weil
    /// jemand eine Seite aufruft.
    /// </para>
    /// <para>
    /// Die Regel selbst bleibt hier und wird nicht beim Aufrufer nachgebaut: Zwei Fassungen davon,
    /// wovon eine im Verwaltungspfad und eine im Renderpfad gilt, wären zwei Gelegenheiten, sie
    /// unterschiedlich zu meinen — und die zweite fiele erst auf, wenn ein Plugin ausgeliefert
    /// wird, das der Guard eigentlich abgelehnt hätte.
    /// </para>
    /// </remarks>
    /// <param name="installation">Die Installation des zu prüfenden Plugins.</param>
    /// <param name="installations">Alle Installationen, einmal geladen.</param>
    /// <param name="workspaceKey">Zielbereich, oder null für global.</param>
    /// <param name="activeInScope">
    /// Die im Workspace aktivierten Plugins, oder null bei globalem Bereich.
    /// </param>
    public CapabilityCheckResult CheckActivation(
        PluginInstallation installation,
        IReadOnlyList<PluginInstallation> installations,
        string? workspaceKey,
        IReadOnlySet<string>? activeInScope)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(installations);

        var required = installation.GetRequiredCapabilities();
        if (required.Count == 0)
        {
            return CapabilityCheckResult.Allowed;
        }

        foreach (var capability in required)
        {
            if (HasActiveProvider(installations, installation.PluginId, capability, workspaceKey, activeInScope))
            {
                continue;
            }

            var scopeSuffix = workspaceKey is null ? "." : $" in workspace '{workspaceKey}'.";
            return CapabilityCheckResult.Denied(
                $"Plugin '{installation.PluginId}' requires capability '{capability}', but no active plugin provides it{scopeSuffix}",
                new Dictionary<string, string>
                {
                    ["requiredCapability"] = capability
                });
        }

        return CapabilityCheckResult.Allowed;
    }

    /// <summary>
    /// Checks whether one plugin can be deactivated without breaking active dependents.
    /// Denies when another active plugin requires a capability only this plugin provides.
    /// </summary>
    public async Task<CapabilityCheckResult> CheckDeactivationAsync(
        string pluginId,
        string? workspaceKey,
        CancellationToken cancellationToken)
    {
        var installation = await installationRepository
            .GetByPluginIdAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (installation is null)
        {
            return CapabilityCheckResult.Allowed;
        }

        var provided = installation.GetProvidedCapabilities();
        var conditional = installation.GetConditionalCapabilities();
        if (provided.Count == 0 && conditional.Count == 0)
        {
            return CapabilityCheckResult.Allowed;
        }

        var activeInScope = await LoadActiveInScopeAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var dependent in installations)
        {
            if (IsSamePlugin(dependent.PluginId, pluginId) || dependent.State == PluginInstallationState.Uninstalled)
            {
                continue;
            }

            var requiredByDependent = dependent.GetRequiredCapabilities();
            if (requiredByDependent.Count == 0)
            {
                continue;
            }

            if (!IsActiveInScope(dependent, workspaceKey, activeInScope))
            {
                continue;
            }

            foreach (var capability in requiredByDependent)
            {
                if (!Provides(installation, capability, workspaceKey))
                {
                    continue;
                }

                var hasAlternative = HasActiveProvider(
                    installations,
                    excludedPluginId: pluginId,
                    capability: capability,
                    workspaceKey: workspaceKey,
                    activeInScope: activeInScope);
                if (hasAlternative)
                {
                    continue;
                }

                var scopeSuffix = workspaceKey is null ? "." : $" in workspace '{workspaceKey}'.";
                return CapabilityCheckResult.Denied(
                    $"Plugin '{pluginId}' provides capability '{capability}' required by active plugin '{dependent.PluginId}'{scopeSuffix}",
                    new Dictionary<string, string>
                    {
                        ["capability"] = capability,
                        ["dependentPluginId"] = dependent.PluginId
                    });
            }
        }

        return CapabilityCheckResult.Allowed;
    }

    /// <summary>
    /// Loads the plugins actually activated in the workspace. Returns <c>null</c> for global
    /// scope, where activation is read from the installation state instead.
    /// </summary>
    private async Task<IReadOnlySet<string>?> LoadActiveInScopeAsync(
        string? workspaceKey,
        CancellationToken cancellationToken)
    {
        if (workspaceKey is null)
        {
            return null;
        }

        var active = await activationReader
            .ListActivePluginIdsAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);
        return active.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private bool HasActiveProvider(
        IReadOnlyList<PluginInstallation> installations,
        string excludedPluginId,
        string capability,
        string? workspaceKey,
        IReadOnlySet<string>? activeInScope)
    {
        foreach (var candidate in installations)
        {
            if (IsSamePlugin(candidate.PluginId, excludedPluginId) ||
                candidate.State == PluginInstallationState.Uninstalled)
            {
                continue;
            }

            if (!Provides(candidate, capability, workspaceKey))
            {
                continue;
            }

            if (IsActiveInScope(candidate, workspaceKey, activeInScope))
            {
                return true;
            }
        }

        return false;
    }

    // A plugin provides a capability if it declares it statically, or declares it conditionally and the
    // runtime-capability registry currently reports it satisfied in the target scope (health-derived).
    private bool Provides(PluginInstallation candidate, string capability, string? workspaceKey)
    {
        if (candidate.GetProvidedCapabilities().Contains(capability, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return runtimeCapabilities is not null
            && candidate.GetConditionalCapabilities().Contains(capability, StringComparer.OrdinalIgnoreCase)
            && IsConditionallySatisfied(candidate.PluginId, capability, workspaceKey);
    }

    // Scope matching (spec §8): a global grant covers every workspace; a workspace consumer is also
    // satisfied by a grant in its own workspace. A global consumer requires a global grant.
    private bool IsConditionallySatisfied(string pluginId, string capability, string? workspaceKey) =>
        runtimeCapabilities!.IsSatisfied(pluginId, capability, workspaceKey: null)
        || (workspaceKey is not null && runtimeCapabilities.IsSatisfied(pluginId, capability, workspaceKey));

    private static bool IsActiveInScope(
        PluginInstallation installation,
        string? workspaceKey,
        IReadOnlySet<string>? activeInScope)
    {
        if (workspaceKey is null)
        {
            return installation.State == PluginInstallationState.Active;
        }

        return activeInScope is not null && activeInScope.Contains(installation.PluginId);
    }

    private static bool IsSamePlugin(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
