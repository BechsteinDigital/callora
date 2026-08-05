namespace Callora.Core.Application.Surfaces;

/// <summary>
/// The caller for one surface request together with the cookie the response should
/// carry (ADR-017 §8). A caller is always present — a guest at minimum — while
/// <see cref="Status"/> says whether the surface may serve authenticated access at all.
/// </summary>
/// <param name="Caller">Guest or authenticated caller for this request.</param>
/// <param name="Status">How identity resolution ended.</param>
/// <param name="CookieValue">
/// The protected cookie value to write, or null when the incoming cookie still
/// applies. Non-null exactly when the context was minted or rotated.
/// </param>
/// <param name="CookieExpiresAtUtc">Expiry to set on the cookie, when one is written.</param>
/// <param name="Detail">Host-side diagnostic detail; never returned to the visitor.</param>
public sealed record SurfaceSessionEstablishment(
    SurfaceCaller Caller,
    SurfaceIdentityResolutionStatus Status,
    string? CookieValue = null,
    DateTimeOffset? CookieExpiresAtUtc = null,
    string? Detail = null)
{
    /// <summary>Whether the response must write a cookie.</summary>
    public bool WritesCookie => CookieValue is not null;

    /// <summary>
    /// Whether the surface must refuse authenticated access — it has an identity
    /// provider it cannot currently consult.
    /// </summary>
    public bool IsClosed => Status is not (SurfaceIdentityResolutionStatus.Anonymous
        or SurfaceIdentityResolutionStatus.Authenticated);
}
