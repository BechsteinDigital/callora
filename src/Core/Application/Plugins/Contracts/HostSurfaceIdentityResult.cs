namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// What an <see cref="IHostSurfaceIdentityProvider"/> returns: either
/// <see cref="Anonymous"/> or an identity candidate (ADR-017 §4). The result is a
/// <em>candidate</em> — the host validates, normalises and clamps it before anything
/// downstream sees it, so a provider cannot mint an issuer it does not own, an
/// unbounded lifetime, or claims outside the declared shape.
/// </summary>
/// <param name="IsIdentified">Whether the provider recognised the visitor.</param>
/// <param name="Issuer">
/// Authority vouching for the subject, for example <c>crm.example</c>. Stable identity
/// is <c>Issuer + SubjectId</c>, never the subject alone. The <c>callora.</c> namespace
/// is reserved for the host and rejected from a plugin provider.
/// </param>
/// <param name="SubjectId">Provider-stable identifier of the visitor.</param>
/// <param name="DisplayName">Human-readable name; falls back to the subject id when omitted.</param>
/// <param name="Claims">
/// Namespaced, multi-valued claims such as <c>crm.roles</c>. The host transports them
/// and never interprets one — their meaning belongs to the issuing plugin.
/// </param>
/// <param name="AuthenticationMethod">How the visitor was authenticated, for example <c>password</c> or <c>magic-link</c>.</param>
/// <param name="AuthenticatedAtUtc">When authentication happened.</param>
/// <param name="ExpiresAtUtc">When the provider considers the authentication stale.</param>
public sealed record HostSurfaceIdentityResult(
    bool IsIdentified,
    string? Issuer,
    string? SubjectId,
    string? DisplayName,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Claims,
    string? AuthenticationMethod,
    DateTimeOffset? AuthenticatedAtUtc,
    DateTimeOffset? ExpiresAtUtc)
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> NoClaims =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>
    /// The visitor was not recognised. This is a normal outcome, not a failure: on a
    /// Public or Mixed surface the caller continues as a guest.
    /// </summary>
    public static HostSurfaceIdentityResult Anonymous { get; } =
        new(false, null, null, null, NoClaims, null, null, null);

    /// <summary>
    /// Builds an identity candidate for the host to validate.
    /// </summary>
    /// <param name="issuer">Authority vouching for the subject.</param>
    /// <param name="subjectId">Provider-stable identifier of the visitor.</param>
    /// <param name="authenticationMethod">How the visitor was authenticated.</param>
    /// <param name="authenticatedAtUtc">When authentication happened.</param>
    /// <param name="expiresAtUtc">When the provider considers the authentication stale.</param>
    /// <param name="displayName">Human-readable name; defaults to the subject id.</param>
    /// <param name="claims">Namespaced claims to carry along.</param>
    public static HostSurfaceIdentityResult Identified(
        string issuer,
        string subjectId,
        string authenticationMethod,
        DateTimeOffset authenticatedAtUtc,
        DateTimeOffset expiresAtUtc,
        string? displayName = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? claims = null) =>
        new(
            true,
            issuer,
            subjectId,
            displayName,
            claims ?? NoClaims,
            authenticationMethod,
            authenticatedAtUtc,
            expiresAtUtc);
}
