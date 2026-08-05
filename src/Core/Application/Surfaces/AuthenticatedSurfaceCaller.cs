namespace Callora.Core.Application.Surfaces;

/// <summary>
/// A visitor whose identity an assigned provider — or the host itself — vouched for
/// (ADR-017 §3). Only this case carries a <see cref="SurfaceIdentity"/>, so a
/// consumer cannot read claims without first establishing that authentication
/// actually happened.
/// </summary>
public sealed record AuthenticatedSurfaceCaller : SurfaceCaller
{
    /// <summary>
    /// Creates an authenticated caller.
    /// </summary>
    /// <param name="subject">Normalised issuer + subject of the authenticated visitor.</param>
    /// <param name="identity">The normalised identity attached to that subject.</param>
    public AuthenticatedSurfaceCaller(SurfaceSubject subject, SurfaceIdentity identity)
        : base(subject)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Identity = identity;
    }

    /// <summary>Claims, display name and validity window of the authenticated visitor.</summary>
    public SurfaceIdentity Identity { get; }
}
