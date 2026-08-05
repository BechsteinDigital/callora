namespace Callora.Core.Application.Surfaces;

/// <summary>
/// What travels in the surface cookie, signed and encrypted (ADR-017 §8.2). It
/// carries its own scope so it can be checked against the resolved surface: a cookie
/// is bound to a host, a surface is not, and two surfaces can share one host. Without
/// the scope, a context minted on surface A would silently apply on surface B.
/// </summary>
/// <param name="Version">Envelope format version, so the shape can change without breaking old cookies.</param>
/// <param name="Kind">Whether the id refers to a guest subject or a server-side session.</param>
/// <param name="Id">Guest subject id, or session id.</param>
/// <param name="TenantKey">Tenant the context belongs to.</param>
/// <param name="WorkspaceKey">Workspace the context belongs to.</param>
/// <param name="SurfaceKey">Surface the context belongs to.</param>
/// <param name="Audience">Host the context was minted for.</param>
/// <param name="IssuedAtUtc">When the context was minted.</param>
public sealed record SurfaceSessionEnvelope(
    int Version,
    SurfaceSessionEnvelopeKind Kind,
    string Id,
    string TenantKey,
    string WorkspaceKey,
    string SurfaceKey,
    string Audience,
    DateTimeOffset IssuedAtUtc)
{
    /// <summary>Current envelope format version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// Whether this envelope was minted for the given surface and host. Checked on
    /// every read; a mismatch means the cookie is discarded, not repaired.
    /// </summary>
    /// <param name="tenantKey">Tenant of the resolved surface.</param>
    /// <param name="workspaceKey">Workspace of the resolved surface.</param>
    /// <param name="surfaceKey">Key of the resolved surface.</param>
    /// <param name="audience">Host the request arrived on.</param>
    public bool MatchesScope(string tenantKey, string workspaceKey, string surfaceKey, string audience) =>
        Version == CurrentVersion &&
        string.Equals(TenantKey, tenantKey, StringComparison.Ordinal) &&
        string.Equals(WorkspaceKey, workspaceKey, StringComparison.Ordinal) &&
        string.Equals(SurfaceKey, surfaceKey, StringComparison.Ordinal) &&
        string.Equals(Audience, audience, StringComparison.OrdinalIgnoreCase);
}
