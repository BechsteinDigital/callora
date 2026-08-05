namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Result of issuing a handoff ticket. The secret exists only in this result and in
/// the response it produces; storage keeps a hash of it (ADR-017 §8.4).
/// </summary>
/// <param name="Status">How the operation ended.</param>
/// <param name="Secret">The single-use secret to present at the target surface.</param>
/// <param name="TargetAudience">Host the ticket may be redeemed on.</param>
/// <param name="TargetSurfaceKey">Surface the ticket may be redeemed at.</param>
/// <param name="ExpiresAtUtc">When the ticket stops being redeemable.</param>
/// <param name="Detail">Host-side diagnostic detail; never returned to the visitor.</param>
public sealed record SurfaceHandoffIssue(
    SurfaceHandoffStatus Status,
    string? Secret = null,
    string? TargetAudience = null,
    string? TargetSurfaceKey = null,
    DateTimeOffset? ExpiresAtUtc = null,
    string? Detail = null)
{
    /// <summary>Refuses the request.</summary>
    /// <param name="status">Why it was refused.</param>
    /// <param name="detail">Host-side diagnostic detail.</param>
    public static SurfaceHandoffIssue Refuse(SurfaceHandoffStatus status, string? detail = null) =>
        new(status, Detail: detail);
}
