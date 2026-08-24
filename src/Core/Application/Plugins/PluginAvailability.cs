namespace Callora.Core.Application.Plugins;

/// <summary>
/// Derived effective availability of a plugin (REV2 §3.2): available only when every
/// observed factor holds. Entitlement participates here, in the derivation — not in the
/// activation write. A billing outage therefore makes a plugin unavailable while the
/// workspace's desired activation is preserved, so restoring the entitlement restores
/// availability without reconfiguration.
/// </summary>
public sealed record PluginAvailability(bool IsAvailable, IReadOnlyList<PluginAvailabilityFactor> UnmetFactors)
{
    /// <summary>
    /// Combines the host-wide factors alone — the question "may this plugin do any work on
    /// this host at all", asked where no workspace is named (platform-wide jobs and events,
    /// plugin-wide routes).
    /// </summary>
    /// <remarks>
    /// The reported <see cref="UnmetFactors"/> stay exact: this verdict names only factors
    /// it actually observed, because the workspace ones are absent from its input type
    /// rather than passed as an unchecked <c>true</c>.
    /// </remarks>
    public static PluginAvailability From(PluginPlatformInputs platform)
    {
        var unmet = CollectPlatform(platform);
        return new PluginAvailability(unmet.Count == 0, unmet);
    }

    /// <summary>
    /// Combines both layers into the workspace verdict: available exactly when no factor of
    /// either layer is unmet. The single canonical derivation — consumers must not
    /// re-implement it.
    /// </summary>
    public static PluginAvailability From(PluginPlatformInputs platform, PluginWorkspaceInputs workspace)
    {
        // The platform layer first and by reuse, not by repetition: a workspace verdict is
        // the platform verdict AND the workspace factors, so there is one place where a
        // platform factor is turned into a verdict, and a ninth factor added there reaches
        // both callers by construction.
        var unmet = CollectPlatform(platform);

        if (!workspace.WorkspaceEnabled)
        {
            unmet.Add(PluginAvailabilityFactor.WorkspaceEnabled);
        }

        if (!workspace.TenantActive)
        {
            unmet.Add(PluginAvailabilityFactor.TenantActive);
        }

        if (!workspace.WorkspaceActive)
        {
            unmet.Add(PluginAvailabilityFactor.WorkspaceActive);
        }

        if (!workspace.RequiredCapabilitiesAvailable)
        {
            unmet.Add(PluginAvailabilityFactor.RequiredCapabilitiesAvailable);
        }

        return new PluginAvailability(unmet.Count == 0, unmet);
    }

    private static List<PluginAvailabilityFactor> CollectPlatform(PluginPlatformInputs platform)
    {
        var unmet = new List<PluginAvailabilityFactor>();
        if (!platform.BundledOrInstalled)
        {
            unmet.Add(PluginAvailabilityFactor.BundledOrInstalled);
        }

        if (!platform.RuntimeHealthy)
        {
            unmet.Add(PluginAvailabilityFactor.RuntimeHealthy);
        }

        if (!platform.Entitled)
        {
            unmet.Add(PluginAvailabilityFactor.Entitled);
        }

        if (!platform.WithinFaultBudget)
        {
            unmet.Add(PluginAvailabilityFactor.WithinFaultBudget);
        }

        return unmet;
    }
}
