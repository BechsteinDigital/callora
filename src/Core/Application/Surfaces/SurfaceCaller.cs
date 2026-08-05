namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Who is using a surface right now. A caller <em>always</em> exists — the common
/// case sits between "anonymous" and "logged in": the recognised guest with a cart,
/// a draft or a half-filled form (ADR-017 §3).
/// <para>
/// The two states are distinguished by <em>type</em>, not by convention. If both
/// arrived as "has a subject", something would eventually check for presence instead
/// of authentication and hang an entitlement off a guest token anyone can mint. To
/// reach an identity a consumer must match <see cref="AuthenticatedSurfaceCaller"/>.
/// </para>
/// The hierarchy is closed: the constructor is <c>private protected</c>, so no third
/// case can appear outside this assembly.
/// </summary>
public abstract record SurfaceCaller
{
    private protected SurfaceCaller(SurfaceSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        Subject = subject;
    }

    /// <summary>
    /// Stable, cookie-bound subject. Present for guests and authenticated callers
    /// alike — it is the key a plugin hangs state off, not an authorisation.
    /// </summary>
    public SurfaceSubject Subject { get; }
}
