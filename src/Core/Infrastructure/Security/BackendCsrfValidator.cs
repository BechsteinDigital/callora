using Callora.Core.Extensibility;

namespace Callora.Core.Infrastructure.Security;

/// <summary>
/// Origin/Referer CSRF guard for cookie-authenticated state changes. The auth
/// cookie is the only credential a browser auto-attaches cross-site, so only
/// unsafe methods carrying that cookie are checked; Bearer/API-key requests
/// (no cookie) are never rejected. A checked request passes only when its source
/// origin (the <c>Origin</c> header, else the <c>Referer</c>) is same-origin or
/// explicitly allowed — otherwise it is rejected fail-closed. Layered on top of
/// the auth cookie's <c>SameSite=Lax</c> as defense in depth. UI hiding is not a
/// security boundary; server-side enforcement stays authoritative.
/// </summary>
[CalloraInternal("CSRF enforcement — not a plugin contract")]
public static class BackendCsrfValidator
{
    private static readonly HashSet<string> SafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    /// <summary>
    /// Returns <c>true</c> when a cookie-authenticated, state-changing request
    /// must be rejected as a probable cross-site request forgery.
    /// </summary>
    /// <param name="method">The HTTP method of the request.</param>
    /// <param name="hasAuthCookie">Whether the admin auth cookie is present.</param>
    /// <param name="originHeader">The request's <c>Origin</c> header, if any.</param>
    /// <param name="refererHeader">The request's <c>Referer</c> header, if any.</param>
    /// <param name="requestOrigin">The host's own origin (<c>scheme://host[:port]</c>).</param>
    /// <param name="allowedOrigins">Extra origins accepted beyond same-origin.</param>
    public static bool IsForbidden(
        string method,
        bool hasAuthCookie,
        string? originHeader,
        string? refererHeader,
        string requestOrigin,
        IReadOnlyCollection<string> allowedOrigins)
    {
        ArgumentNullException.ThrowIfNull(allowedOrigins);

        if (string.IsNullOrEmpty(method) || SafeMethods.Contains(method) || !hasAuthCookie)
        {
            return false;
        }

        // Source origin: prefer the Origin header, fall back to the Referer's origin.
        var source = NormalizeOrigin(originHeader) ?? NormalizeOrigin(refererHeader);
        if (source is null)
        {
            // A cookie-authenticated mutation with no verifiable source is rejected.
            return true;
        }

        if (OriginEquals(source, requestOrigin))
        {
            return false;
        }

        foreach (var allowed in allowedOrigins)
        {
            if (OriginEquals(source, allowed))
            {
                return false;
            }
        }

        return true;
    }

    private static bool OriginEquals(string normalizedSource, string? candidate)
    {
        var normalizedCandidate = NormalizeOrigin(candidate);
        return normalizedCandidate is not null &&
               string.Equals(normalizedSource, normalizedCandidate, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reduces an Origin/Referer/own-origin value to a canonical
    /// <c>scheme://authority</c> (default ports normalized away), or <c>null</c>
    /// when it is absent or unparseable (including the literal "null" origin).
    /// </summary>
    private static string? NormalizeOrigin(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            ? $"{uri.Scheme}://{uri.Authority}"
            : null;
    }
}
