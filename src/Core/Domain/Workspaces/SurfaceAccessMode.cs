namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// Access policy of a surface (ADR-014 §6.1): who may reach it.
/// </summary>
public enum SurfaceAccessMode
{
    /// <summary>Reachable without authentication (e.g. a public website).</summary>
    Public = 0,

    /// <summary>Requires a valid authentication (e.g. a dialer or agent desktop).</summary>
    Authenticated = 1,

    /// <summary>Has both public and protected routes (e.g. a site with a customer area).</summary>
    Mixed = 2,
}
