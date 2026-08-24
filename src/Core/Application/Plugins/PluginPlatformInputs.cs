namespace Callora.Core.Application.Plugins;

/// <summary>
/// Observed truth of the availability factors that hold host-wide, independent of any
/// workspace: the plugin is installed, its runtime is healthy, an entitlement covers it,
/// and it has not exceeded its fault budget.
/// </summary>
/// <remarks>
/// <para>
/// These four are not "the factors that happen to work without a workspace" — they are the
/// ones that must hold in <b>every</b> workspace. A plugin that is uninstalled, faulted,
/// unentitled or over budget is available nowhere. The platform verdict is therefore the
/// <b>precondition</b> of a workspace verdict, not a weaker variant of it, which is why
/// <see cref="PluginAvailability.From(PluginPlatformInputs, PluginWorkspaceInputs)"/> is a
/// conjunction of the two layers rather than a second derivation.
/// </para>
/// <para>
/// Splitting the inputs is what keeps the combination honest. A platform verdict that
/// claims <see cref="PluginAvailabilityFactor.WorkspaceEnabled"/> is not merely discouraged
/// here, it is unconstructible: the field does not exist on this type.
/// </para>
/// </remarks>
public readonly record struct PluginPlatformInputs(
    bool BundledOrInstalled,
    bool RuntimeHealthy,
    bool Entitled,
    bool WithinFaultBudget = true);
