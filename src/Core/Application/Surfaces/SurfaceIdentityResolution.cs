namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Result of resolving who is calling a surface (ADR-017 §6).
/// </summary>
/// <param name="Status">How resolution ended.</param>
/// <param name="Caller">The authenticated caller when one was established.</param>
/// <param name="Detail">
/// Host-side diagnostic detail for logs and the admin view. Never returned to the
/// visitor: it would tell an attacker which provider is misconfigured.
/// </param>
public sealed record SurfaceIdentityResolution(
    SurfaceIdentityResolutionStatus Status,
    AuthenticatedSurfaceCaller? Caller = null,
    string? Detail = null)
{
    /// <summary>Nobody was recognised; a guest may continue.</summary>
    public static SurfaceIdentityResolution Anonymous { get; } =
        new(SurfaceIdentityResolutionStatus.Anonymous);

    /// <summary>
    /// Whether the surface must refuse authenticated access. True exactly when the
    /// surface has an identity provider it cannot currently consult — the case where
    /// falling back to anonymous would silently widen access.
    /// </summary>
    public bool IsClosed => Status is not (SurfaceIdentityResolutionStatus.Anonymous
        or SurfaceIdentityResolutionStatus.Authenticated);

    /// <summary>An identity was established.</summary>
    /// <param name="caller">The normalised caller.</param>
    public static SurfaceIdentityResolution Authenticated(AuthenticatedSurfaceCaller caller) =>
        new(SurfaceIdentityResolutionStatus.Authenticated, caller);

    /// <summary>The surface cannot answer who is calling.</summary>
    /// <param name="status">Which failure occurred.</param>
    /// <param name="detail">Host-side diagnostic detail.</param>
    public static SurfaceIdentityResolution Closed(
        SurfaceIdentityResolutionStatus status,
        string? detail = null) =>
        new(status, null, detail);
}
