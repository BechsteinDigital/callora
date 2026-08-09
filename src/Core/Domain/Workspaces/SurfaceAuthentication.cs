namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// Which authentication applies on a surface node (ADR-023).
/// <para>
/// The axis used to answer "must one be signed in?" — with a third value, <c>Mixed</c>, for
/// "somewhere below here something is protected". That value described a subtree, and since
/// ADR-019 the subtree describes itself: every node carries its own. What is left is the
/// question the host was already answering in secret — which login it sends a visitor to.
/// </para>
/// </summary>
public enum SurfaceAuthentication
{
    /// <summary>No authentication required. Visitors are guests (e.g. a public website).</summary>
    Public = 0,

    /// <summary>
    /// The identity plugin assigned to the surface (ADR-017 §5.2). Unauthenticated requests are
    /// refused with 401 — the host has no login to offer here, the plugin owns that flow.
    /// </summary>
    SurfaceIdentity = 1,

    /// <summary>
    /// The host's own operator sign-in. Unauthenticated requests are redirected to the admin
    /// login, and the operator's RBAC permissions apply as surface claims — which is what makes
    /// a surface usable as an operating tool rather than a customer-facing one.
    /// </summary>
    Administration = 2,
}
