using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Contributes composable views to a surface's slots (#125 block C).
/// <para>
/// The declaration lives on the server even though the component lives in the browser,
/// and that split is deliberate. The server decides which views a slot actually holds,
/// so ordering, cardinality and claim-based visibility are decided before any markup
/// reaches the visitor. The plugin's bundle then registers the Vue component under the
/// same view id and the runtime mounts it into the island the server emitted.
/// </para>
/// </summary>
[CalloraExtensible("Extension point — implement to contribute views to surface slots (#125 block C)")]
public interface IHostSurfaceViewContributor
{
    /// <summary>Stable plugin identifier owning these views.</summary>
    string PluginId { get; }

    /// <summary>Declared views and the slots they fill.</summary>
    IReadOnlyList<HostSurfaceViewRegistration> Views { get; }

    /// <summary>
    /// Navigation entries this plugin contributes to the surface. Empty for a plugin
    /// that only fills slots. The theme decides how these are presented, so what is
    /// declared here is meaning rather than placement.
    /// </summary>
    IReadOnlyList<HostSurfaceNavigationItem> NavigationItems => [];
}
