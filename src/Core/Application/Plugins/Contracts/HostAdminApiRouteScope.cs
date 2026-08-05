namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Whether a plugin Admin API route acts inside one workspace or across the
/// platform. Governs the host's pre-dispatch gate (#109).
/// </summary>
public enum HostAdminApiRouteScope
{
    /// <summary>
    /// The route operates on workspace-scoped data — the default, and the safe
    /// one. The host resolves the effective workspace (the caller's bound
    /// workspace, or the one a platform operator names via
    /// <c>?workspaceKey=</c>), rejects the request when none is resolvable, and
    /// dispatches only while the plugin is effectively available there.
    /// </summary>
    Workspace = 0,

    /// <summary>
    /// The route carries no workspace at all — plugin-wide status or metadata.
    /// An explicit opt-out of the workspace gate: declare it only when the
    /// handler genuinely reads nothing workspace-scoped.
    /// </summary>
    Global = 1
}
