using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Derives a surface identity from an already-authenticated backend principal
/// (ADR-017 §7). It exists because a surface has two legitimate kinds of user:
/// internal ones — an agent at their desktop, a doctor at a practice workstation —
/// who are platform users already, and external ones who need a plugin. Making
/// employees authenticate a second time through a plugin would be absurd.
/// <para>
/// It is strictly subordinate: a surface with an assigned plugin provider never
/// consults this source, otherwise two identities would be valid at once.
/// </para>
/// <para>
/// Whether the operator's RBAC permissions travel as surface claims depends on the node's
/// authentication (ADR-023). ADR-017 §7 banned it outright, and rightly so while this source
/// applied to every surface that merely lacked a plugin — a public website would have handed
/// admin rights to whoever happened to be signed in elsewhere. <see cref="SurfaceAuthentication.Administration"/>
/// makes it a declared choice on one node instead of a side effect of an absent assignment.
/// </para>
/// </summary>
public interface ISurfaceHostIdentitySource
{
    /// <summary>
    /// Derives an identity from the current backend principal, or
    /// <see cref="HostSurfaceIdentityResult.Anonymous"/> when there is none.
    /// </summary>
    /// <param name="request">Surface context of the current request.</param>
    /// <param name="authentication">
    /// The node's declared authentication. Only <see cref="SurfaceAuthentication.Administration"/>
    /// turns operator permissions into surface claims.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        SurfaceAuthentication authentication,
        CancellationToken cancellationToken = default);
}
