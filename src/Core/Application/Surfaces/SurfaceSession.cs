namespace Callora.Core.Application.Surfaces;

/// <summary>
/// A live authenticated surface session: the normalised identity plus the scope it
/// was minted for (ADR-017 §8). The scope travels with it so it can be checked on
/// every use — a session is valid for one tenant, workspace, surface and audience,
/// never "wherever the cookie happens to be sent".
/// </summary>
/// <param name="SessionId">Opaque id; the only part that travels in the cookie.</param>
/// <param name="TenantKey">Tenant the session belongs to.</param>
/// <param name="WorkspaceKey">Workspace the session belongs to.</param>
/// <param name="SurfaceKey">Surface the session belongs to.</param>
/// <param name="Audience">Host the session was minted for.</param>
/// <param name="Subject">Issuer + subject of the authenticated visitor.</param>
/// <param name="Identity">Display name, claims and validity window.</param>
/// <param name="IssuedAtUtc">When the host minted the session.</param>
/// <param name="ExpiresAtUtc">When the session stops being valid.</param>
/// <param name="IdentityPluginId">Provider that vouched for it, or null for the host source.</param>
/// <param name="IdentityVersion">Version of that provider at issue time.</param>
public sealed record SurfaceSession(
    Guid SessionId,
    string TenantKey,
    string WorkspaceKey,
    string SurfaceKey,
    string Audience,
    SurfaceSubject Subject,
    SurfaceIdentity Identity,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string? IdentityPluginId,
    string? IdentityVersion);
