namespace Callora.Surface.Rendering;

/// <summary>
/// The caller as a template may see it (ADR-015 §8, ADR-017 §9): plain allowlisted
/// values, never a .NET object and never the session token. A template can tell who
/// is looking at the page; it cannot pass their session on.
/// </summary>
/// <param name="State">Either <c>guest</c> or <c>authenticated</c>.</param>
/// <param name="Issuer">Authority vouching for the subject; part of the identity, never dropped.</param>
/// <param name="SubjectId">Stable subject within that issuer.</param>
/// <param name="DisplayName">Human-readable name; empty for a guest.</param>
/// <param name="Claims">Namespaced claims, empty for a guest.</param>
/// <param name="ClaimsJson">The same claims as JSON, for handing to the browser through a data attribute.</param>
public sealed record SurfaceCallerView(
    string State,
    string Issuer,
    string SubjectId,
    string DisplayName,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Claims,
    string ClaimsJson)
{
    /// <summary>Template value for an unauthenticated caller.</summary>
    public const string GuestState = "guest";

    /// <summary>Template value for an authenticated caller.</summary>
    public const string AuthenticatedState = "authenticated";
}
