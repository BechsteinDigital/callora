namespace Callora.Core.Application.Surfaces;

/// <summary>
/// A recognised but unauthenticated visitor (ADR-017 §3). Its subject is stable
/// across requests, which is what makes an anonymous cart or a multi-step form
/// possible — but it proves nothing: anyone can obtain one by visiting the surface.
/// </summary>
public sealed record GuestSurfaceCaller : SurfaceCaller
{
    /// <summary>
    /// Creates a guest caller for an already-minted guest subject.
    /// </summary>
    /// <param name="subject">Subject issued under <see cref="SurfaceIdentityIssuers.Guest"/>.</param>
    public GuestSurfaceCaller(SurfaceSubject subject)
        : base(subject)
    {
    }
}
