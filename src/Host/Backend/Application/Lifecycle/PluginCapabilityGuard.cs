using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Application.Plugins;
using Callora.Host.Backend.Domain.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

/// <summary>
/// Validates capability dependencies between plugins before activation and deactivation.
/// A plugin declaring <c>requiresCapabilities</c> can only be activated when another
/// active plugin provides each required capability in the same scope.
/// </summary>
public sealed class PluginCapabilityGuard(
    IPluginInstallationRepository installationRepository,
    IWorkspacePluginActivationReader activationReader)
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
            return CapabilityCheckResult.Allowed;

        var required = installation.GetRequiredCapabilities();
        if (required.Count == 0)
            return CapabilityCheckResult.Allowed;

        var activeInScope = await LoadActiveInScopeAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var capability in required)
        {
            if (HasActiveProvider(installations, pluginId, capability, workspaceKey, activeInScope))
                continue;

            var scopeSuffix = workspaceKey is null ? "." : $" in workspace '{workspaceKey}'.";
            return CapabilityCheckResult.Denied(
                $"Plugin '{pluginId}' requires capability '{capability}', but no active plugin provides it{scopeSuffix}",
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
            return CapabilityCheckResult.Allowed;

        var provided = installation.GetProvidedCapabilities();
        if (provided.Count == 0)
            return CapabilityCheckResult.Allowed;

        var activeInScope = await LoadActiveInScopeAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var dependent in installations)
        {
            if (IsSamePlugin(dependent.PluginId, pluginId) || dependent.State == PluginInstallationState.Uninstalled)
                continue;

            var requiredByDependent = dependent.GetRequiredCapabilities();
            if (requiredByDependent.Count == 0)
                continue;

            if (!IsActiveInScope(dependent, workspaceKey, activeInScope))
                continue;

            foreach (var capability in requiredByDependent)
            {
                if (!provided.Contains(capability, StringComparer.OrdinalIgnoreCase))
                    continue;

                var hasAlternative = HasActiveProvider(
                    installations,
                    excludedPluginId: pluginId,
                    capability: capability,
                    workspaceKey: workspaceKey,
                    activeInScope: activeInScope);
                if (hasAlternative)
                    continue;

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
            return null;

        var active = await activationReader
            .ListActivePluginIdsAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);
        return active.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasActiveProvider(
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

            if (!candidate.GetProvidedCapabilities().Contains(capability, StringComparer.OrdinalIgnoreCase))
                continue;

            if (IsActiveInScope(candidate, workspaceKey, activeInScope))
                return true;
        }

        return false;
    }

    private static bool IsActiveInScope(
        PluginInstallation installation,
        string? workspaceKey,
        IReadOnlySet<string>? activeInScope)
    {
        if (workspaceKey is null)
            return installation.State == PluginInstallationState.Active;

        return activeInScope is not null && activeInScope.Contains(installation.PluginId);
    }

    private static bool IsSamePlugin(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
