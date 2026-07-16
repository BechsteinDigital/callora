namespace Callora.Core.Application.Plugins;

/// <summary>
/// The activation order plus the plugins that cannot be safely ordered.
/// </summary>
/// <param name="Order">Plugin ids in activation order (dependencies before dependents).</param>
/// <param name="UnresolvedDependencies">Plugin ids requiring a capability no installed
/// plugin provides (transitively) — they are left out of <paramref name="Order"/>.</param>
/// <param name="Cyclic">Plugin ids caught in a capability dependency cycle.</param>
internal sealed record PluginActivationPlan(
    IReadOnlyList<string> Order,
    IReadOnlyList<string> UnresolvedDependencies,
    IReadOnlyList<string> Cyclic);
