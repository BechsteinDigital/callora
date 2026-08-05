namespace Callora.Core.Application.Surfaces;

/// <summary>
/// The authenticated part of a surface caller — everything that only exists once a
/// visitor has actually proven who they are (ADR-017 §3). It hangs off a subject
/// rather than replacing it: the subject exists for guests too.
/// </summary>
/// <param name="DisplayName">Human-readable name, never empty after normalisation.</param>
/// <param name="Claims">
/// Namespaced, multi-valued claims the issuing plugin attached. The host transports
/// them and interprets none: what <c>crm.roles</c> means is the CRM's business.
/// </param>
/// <param name="AuthenticationMethod">How the visitor authenticated, for example <c>password</c>.</param>
/// <param name="AuthenticatedAtUtc">When authentication happened.</param>
/// <param name="ExpiresAtUtc">When the identity stops being valid, already clamped to the host maximum.</param>
public sealed record SurfaceIdentity(
    string DisplayName,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Claims,
    string AuthenticationMethod,
    DateTimeOffset AuthenticatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
