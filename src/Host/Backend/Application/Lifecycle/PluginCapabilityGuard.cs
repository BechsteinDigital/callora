using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Domain.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

/// <summary>
/// Validates capability dependencies between plugins before activation and deactivation.
/// A plugin declaring <c>requiresCapabilities</c> can only be activated when another
/// active plugin provides each required capability in the same scope.
/// </summary>
public sealed class PluginCapabilityGuard(
    IPluginInstallationRepository installationRepository,
    IPluginEntitlementStore entitlementStore)
{
    /// <summary>
    /// Checks whether all required capabilities of one plugin are provided in the target scope.
    /// Global scope (<paramref name="workspaceKey"/> null) requires globally active providers;
    /// workspace scope requires providers entitled in the same workspace.
    /// </summary>
    public async Task<CapabilityCheckResult> CheckActivationAsync(
        string pluginId,
        string? workspaceKey,
        CancellationToken cancellationToken,
        string? tenantKey = null)
    {
        var installation = await installationRepository
            .GetByPluginIdAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (installation is null)
            return CapabilityCheckResult.Allowed;

        var required = installation.GetRequiredCapabilities();
        if (required.Count == 0)
            return CapabilityCheckResult.Allowed;

        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var capability in required)
        {
            if (await HasActiveProviderAsync(installations, pluginId, capability, workspaceKey, tenantKey, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

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
        CancellationToken cancellationToken,
        string? tenantKey = null)
    {
        var installation = await installationRepository
            .GetByPluginIdAsync(pluginId, cancellationToken)
            .ConfigureAwait(false);
        if (installation is null)
            return CapabilityCheckResult.Allowed;

        var provided = installation.GetProvidedCapabilities();
        if (provided.Count == 0)
            return CapabilityCheckResult.Allowed;

        var installations = await installationRepository.ListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var dependent in installations)
        {
            if (IsSamePlugin(dependent.PluginId, pluginId) || dependent.State == PluginInstallationState.Uninstalled)
                continue;

            var requiredByDependent = dependent.GetRequiredCapabilities();
            if (requiredByDependent.Count == 0)
                continue;

            if (!await IsActiveInScopeAsync(dependent, workspaceKey, tenantKey, cancellationToken).ConfigureAwait(false))
                continue;

            foreach (var capability in requiredByDependent)
            {
                if (!provided.Contains(capability, StringComparer.OrdinalIgnoreCase))
                    continue;

                var hasAlternative = await HasActiveProviderAsync(
                        installations,
                        excludedPluginId: pluginId,
                        capability: capability,
                        workspaceKey: workspaceKey,
                        tenantKey: tenantKey,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
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

    private async Task<bool> HasActiveProviderAsync(
        IReadOnlyList<PluginInstallation> installations,
        string excludedPluginId,
        string capability,
        string? workspaceKey,
        string? tenantKey,
        CancellationToken cancellationToken)
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

            if (await IsActiveInScopeAsync(candidate, workspaceKey, tenantKey, cancellationToken).ConfigureAwait(false))
                return true;
        }

        return false;
    }

    private async Task<bool> IsActiveInScopeAsync(
        PluginInstallation installation,
        string? workspaceKey,
        string? tenantKey,
        CancellationToken cancellationToken)
    {
        if (workspaceKey is null)
            return installation.State == PluginInstallationState.Active;

        return await entitlementStore
            .IsEntitledAsync(installation.PluginId, workspaceKey, tenantKey, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsSamePlugin(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
