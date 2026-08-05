namespace Callora.Core.Application.Surfaces;

/// <summary>
/// A caller together with the surface scope its cookie was minted for. Seams that
/// resolve the caller from the cookie rather than from a route need both: who is
/// calling, and which surface they are calling as (ADR-017 §9).
/// </summary>
/// <param name="Caller">Guest or authenticated caller.</param>
/// <param name="TenantKey">Tenant the context belongs to.</param>
/// <param name="WorkspaceKey">Workspace the context belongs to.</param>
/// <param name="SurfaceKey">Surface the context belongs to.</param>
public sealed record SurfaceCallerContext(
    SurfaceCaller Caller,
    string TenantKey,
    string WorkspaceKey,
    string SurfaceKey);
