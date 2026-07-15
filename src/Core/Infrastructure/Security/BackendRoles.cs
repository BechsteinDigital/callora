namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Role names recognized by backend authorization policies.
/// </summary>
public static class BackendRoles
{
    /// <summary>
    /// Platform super administrator: unrestricted, global backend access
    /// across every workspace. The only role that satisfies permission
    /// checks unconditionally and counts as a platform operator.
    /// </summary>
    public const string SuperAdmin = "superadmin";

    /// <summary>
    /// Workspace administrator. Not a global operator — it grants
    /// workspace-scoped rights only, carried per workspace through
    /// <see cref="Callora.Core.Domain.Workspaces.WorkspaceMembership"/>.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Role used by API-key based host access; treated as a platform operator.
    /// </summary>
    public const string HostApi = "host.api";
}
