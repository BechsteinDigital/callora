using Callora.Core.Application.Plugins.Contracts;

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
/// The derived identity carries subject, display name and workspace membership —
/// <strong>never admin permissions as claims</strong>, or a plugin would eventually
/// check them and escalate rights that were never meant for the surface.
/// </para>
/// </summary>
public interface ISurfaceHostIdentitySource
{
    /// <summary>
    /// Derives an identity from the current backend principal, or
    /// <see cref="HostSurfaceIdentityResult.Anonymous"/> when there is none.
    /// </summary>
    /// <param name="request">Surface context of the current request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask<HostSurfaceIdentityResult> AuthenticateAsync(
        HostSurfaceIdentityRequest request,
        CancellationToken cancellationToken = default);
}
