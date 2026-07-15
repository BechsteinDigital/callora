namespace Callora.Core.Application.Plugins;

/// <summary>
/// Derived effective availability of a plugin in a workspace (REV2 §3.2):
/// available only when every factor holds. Entitlement participates here, in the
/// derivation — not in the activation write. A billing outage therefore makes a
/// plugin unavailable while the workspace's desired activation is preserved, so
/// restoring the entitlement restores availability without reconfiguration.
/// </summary>
public sealed record PluginAvailability(bool IsAvailable, IReadOnlyList<PluginAvailabilityFactor> UnmetFactors)
{
    /// <summary>
    /// Combines the observed factors: available exactly when none are unmet.
    /// The single canonical derivation — consumers must not re-implement it.
    /// </summary>
    public static PluginAvailability From(PluginAvailabilityInputs inputs)
    {
        var unmet = new List<PluginAvailabilityFactor>();
        if (!inputs.BundledOrInstalled) unmet.Add(PluginAvailabilityFactor.BundledOrInstalled);
        if (!inputs.RuntimeHealthy) unmet.Add(PluginAvailabilityFactor.RuntimeHealthy);
        if (!inputs.Entitled) unmet.Add(PluginAvailabilityFactor.Entitled);
        if (!inputs.WorkspaceEnabled) unmet.Add(PluginAvailabilityFactor.WorkspaceEnabled);
        if (!inputs.TenantActive) unmet.Add(PluginAvailabilityFactor.TenantActive);
        if (!inputs.WorkspaceActive) unmet.Add(PluginAvailabilityFactor.WorkspaceActive);
        if (!inputs.RequiredCapabilitiesAvailable) unmet.Add(PluginAvailabilityFactor.RequiredCapabilitiesAvailable);

        return new PluginAvailability(unmet.Count == 0, unmet);
    }
}
