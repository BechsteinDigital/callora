using Callora.Core.Application.Surfaces;
using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Surfaces;

/// <summary>
/// Reads and writes the surface context cookie (ADR-017 §8.2). Kept in one place so
/// every seam that establishes a caller — rendering, the surface API, WebSocket
/// upgrades — agrees on the cookie's name and its protection attributes.
/// </summary>
public sealed class SurfaceSessionCookieAccessor(SurfaceIdentityOptions options)
{
    /// <summary>Reads the incoming surface cookie, or null when absent.</summary>
    /// <param name="httpContext">Current request.</param>
    public string? Read(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.Request.Cookies.TryGetValue(options.CookieName, out var value) ? value : null;
    }

    /// <summary>
    /// Writes the cookie an establishment asked for; does nothing when the incoming
    /// cookie still applies.
    /// </summary>
    /// <param name="httpContext">Current request.</param>
    /// <param name="establishment">Result of establishing the caller.</param>
    public void Write(HttpContext httpContext, SurfaceSessionEstablishment establishment)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(establishment);

        if (establishment.CookieValue is not { } value)
        {
            return;
        }

        httpContext.Response.Cookies.Append(options.CookieName, value, new CookieOptions
        {
            // Not readable from script: the envelope is the visitor's context, not
            // their data — and a token no script can read cannot be exfiltrated by one.
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            Expires = establishment.CookieExpiresAtUtc,
        });
    }
}
