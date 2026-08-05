namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Outcome of validating one provider identity candidate: either an accepted
/// caller, or a reason the host refused it (ADR-017 §4). "Not identified" is a
/// separate outcome from "rejected" — the first continues as a guest where the
/// access mode allows it, the second is a provider failure.
/// </summary>
/// <param name="Caller">The accepted caller, or <see langword="null"/>.</param>
/// <param name="Reason">Why the candidate was refused, or <see cref="SurfaceIdentityRejectionReason.None"/>.</param>
/// <param name="Detail">Host-side diagnostic detail; never returned to the visitor.</param>
internal sealed record SurfaceIdentityNormalization(
    AuthenticatedSurfaceCaller? Caller,
    SurfaceIdentityRejectionReason Reason,
    string? Detail = null)
{
    /// <summary>Whether a caller was produced.</summary>
    public bool IsAccepted => Caller is not null;

    /// <summary>Accepts a normalised caller.</summary>
    /// <param name="caller">The caller to accept.</param>
    public static SurfaceIdentityNormalization Accept(AuthenticatedSurfaceCaller caller) =>
        new(caller, SurfaceIdentityRejectionReason.None);

    /// <summary>Refuses the candidate.</summary>
    /// <param name="reason">Why it was refused.</param>
    /// <param name="detail">Host-side diagnostic detail.</param>
    public static SurfaceIdentityNormalization Reject(
        SurfaceIdentityRejectionReason reason,
        string? detail = null) =>
        new(null, reason, detail);

    /// <summary>The provider recognised nobody.</summary>
    public static SurfaceIdentityNormalization NotIdentified { get; } =
        new(null, SurfaceIdentityRejectionReason.NotIdentified);
}
