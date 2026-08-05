namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Result of reading or changing a surface's identity provider assignment.
/// </summary>
/// <param name="Status">How the operation ended.</param>
/// <param name="Assignment">The stored assignment when the operation succeeded.</param>
/// <param name="Message">Human-readable detail for the operator.</param>
/// <param name="RevokedSessions">
/// How many surface sessions the change invalidated. A provider change always ends
/// them: if a different party now vouches for the surface's visitors, carrying the
/// old trust over would be inconsistent (ADR-017 §6.3).
/// </param>
public sealed record SurfaceIdentityAssignmentResult(
    SurfaceIdentityAssignmentStatus Status,
    SurfaceIdentityAssignment? Assignment = null,
    string? Message = null,
    int RevokedSessions = 0);
