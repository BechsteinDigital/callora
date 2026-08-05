namespace Callora.Core.Domain.Surfaces;

/// <summary>
/// A server-side surface session (ADR-017 §8.1). Only authenticated callers get a
/// row: a guest context lives entirely in its signed cookie, because it carries no
/// authority and a database write per anonymous page view would turn every public
/// surface into an amplification target. What has authority is stored, and can
/// therefore be revoked.
/// </summary>
public sealed class SurfaceSessionRecord
{
    /// <summary>Opaque session id; the only part that travels in the cookie.</summary>
    public Guid Id { get; set; }

    public string TenantKey { get; set; } = string.Empty;

    public string WorkspaceKey { get; set; } = string.Empty;

    public string SurfaceKey { get; set; } = string.Empty;

    /// <summary>
    /// Host the session was minted for. A cookie is host-bound, a surface is not —
    /// so the binding is checked explicitly rather than assumed.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Authority vouching for the subject; never dropped from the identity.</summary>
    public string Issuer { get; set; } = string.Empty;

    public string SubjectId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Namespaced claims as JSON. Transported verbatim, never interpreted.</summary>
    public string ClaimsJson { get; set; } = "{}";

    public string AuthenticationMethod { get; set; } = string.Empty;

    public DateTimeOffset AuthenticatedAtUtc { get; set; }

    public DateTimeOffset IssuedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    /// <summary>
    /// Provider that vouched for this session. Kept as provenance: when the surface's
    /// assignment changes, a session issued under the previous provider is no longer
    /// trusted (ADR-017 §6.3).
    /// </summary>
    public string? IdentityPluginId { get; set; }

    /// <summary>Version of that provider at issue time.</summary>
    public string? IdentityVersion { get; set; }
}
