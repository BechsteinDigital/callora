using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Contributes HTTP routes that a surface's own visitors may call (#125 block B).
/// <para>
/// It sits between the two seams that existed before: the Admin API is
/// authenticated, permission-gated and workspace-scoped but speaks for an operator,
/// while the public HTTP seam is anonymous. Neither lets a plugin act in the name of
/// an ordinary CRM, patient or portal user, which is what this one is for.
/// </para>
/// <para>
/// Routes are mounted under <c>/surface-api/{pluginId}/…</c> on the surface's own
/// host. The plugin id in the path is what keeps two plugins from colliding; within
/// a plugin, a duplicate method and template pair is a declaration error and the
/// duplicate is not mounted.
/// </para>
/// </summary>
[CalloraExtensible("Extension point — implement to contribute routes a surface's visitors may call (#125 block B)")]
public interface IHostSurfaceApiContributor
{
    /// <summary>Stable plugin identifier owning these routes.</summary>
    string PluginId { get; }

    /// <summary>Declared surface API routes handled by the plugin.</summary>
    IReadOnlyList<HostSurfaceApiRouteRegistration> Routes { get; }
}
