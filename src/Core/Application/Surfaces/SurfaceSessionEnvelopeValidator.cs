namespace Callora.Core.Application.Surfaces;

/// <summary>
/// The checks every seam runs before believing a surface cookie (ADR-017 §8.2, §6.3).
/// They live in one place because rendering, the WebSocket gate and the handoff
/// exchange all have to agree: a cookie that one seam discards must not be honoured
/// by another.
/// </summary>
internal static class SurfaceSessionEnvelopeValidator
{
    /// <summary>
    /// Whether an envelope may be used for the given surface, host and instant.
    /// </summary>
    /// <param name="envelope">The unprotected envelope.</param>
    /// <param name="tenantKey">Tenant of the surface the request is for.</param>
    /// <param name="workspaceKey">Workspace of that surface.</param>
    /// <param name="surfaceKey">Key of that surface.</param>
    /// <param name="audience">Host the request arrived on.</param>
    /// <param name="identityAssignedAtUtc">When the surface's identity provider was last assigned.</param>
    /// <param name="nowUtc">Current instant.</param>
    /// <param name="guestLifetime">How long a guest context stays valid.</param>
    public static bool IsUsable(
        SurfaceSessionEnvelope envelope,
        string tenantKey,
        string workspaceKey,
        string surfaceKey,
        string audience,
        DateTimeOffset? identityAssignedAtUtc,
        DateTimeOffset nowUtc,
        TimeSpan guestLifetime)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // A cookie is host-bound, a surface is not, and two surfaces can share a host.
        // A scope mismatch is discarded rather than repaired.
        if (!envelope.MatchesScope(tenantKey, workspaceKey, surfaceKey, audience))
        {
            return false;
        }

        if (envelope.Kind == SurfaceSessionEnvelopeKind.Guest)
        {
            return envelope.IssuedAtUtc + guestLifetime > nowUtc;
        }

        // A change of identity provider voids everything issued before it: if another
        // party now vouches for the surface's visitors, carrying trust over would be
        // inconsistent. Guests are unaffected, they vouch for nothing.
        return identityAssignedAtUtc is not { } assignedAt || envelope.IssuedAtUtc >= assignedAt;
    }
}
