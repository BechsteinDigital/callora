namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Outcome of validating a candidate's claim bag (ADR-017 §3.1).
/// </summary>
/// <param name="Claims">The accepted claims, or <see langword="null"/> on rejection.</param>
/// <param name="Reason">Why the bag was refused, or <see cref="SurfaceIdentityRejectionReason.None"/>.</param>
/// <param name="Detail">Host-side diagnostic detail; never returned to the visitor.</param>
internal sealed record SurfaceIdentityClaimNormalization(
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Claims,
    SurfaceIdentityRejectionReason Reason,
    string? Detail = null)
{
    /// <summary>Accepts a validated claim bag.</summary>
    /// <param name="claims">The claims to accept.</param>
    public static SurfaceIdentityClaimNormalization Accept(
        IReadOnlyDictionary<string, IReadOnlyList<string>> claims) =>
        new(claims, SurfaceIdentityRejectionReason.None);

    /// <summary>Refuses the claim bag.</summary>
    /// <param name="reason">Why it was refused.</param>
    /// <param name="detail">Host-side diagnostic detail.</param>
    public static SurfaceIdentityClaimNormalization Reject(
        SurfaceIdentityRejectionReason reason,
        string? detail = null) =>
        new(null, reason, detail);
}
