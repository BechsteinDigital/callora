using Callora.Core.Application.Workspaces;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Result of redeeming a handoff ticket: the target surface and the caller to
/// establish on it (ADR-017 §8.4).
/// </summary>
/// <param name="Status">How the redemption ended.</param>
/// <param name="Surface">The target surface, when the ticket was valid.</param>
/// <param name="Caller">The identity the ticket carried.</param>
/// <param name="Detail">Host-side diagnostic detail; never returned to the visitor.</param>
public sealed record SurfaceHandoffRedemption(
    SurfaceHandoffStatus Status,
    WorkspaceSurfaceSnapshot? Surface = null,
    AuthenticatedSurfaceCaller? Caller = null,
    string? Detail = null)
{
    /// <summary>Refuses the redemption.</summary>
    /// <param name="status">Why it was refused.</param>
    /// <param name="detail">Host-side diagnostic detail.</param>
    public static SurfaceHandoffRedemption Refuse(SurfaceHandoffStatus status, string? detail = null) =>
        new(status, Detail: detail);
}
