using Callora.Core.Application.Surfaces;

namespace Callora.Surface.Rendering.Api.SurfaceContext;

/// <summary>
/// Re-reads the caller behind a surface cookie. Returns null when the session no longer holds —
/// signed out, expired, bound to another host, or predating the surface's current identity
/// provider (ADR-017 §6.3). All four mean the same thing to an open connection.
/// </summary>
public delegate Task<SurfaceCallerContext?> SurfaceSessionProbe(
    string cookieValue,
    string audience,
    CancellationToken cancellationToken);
