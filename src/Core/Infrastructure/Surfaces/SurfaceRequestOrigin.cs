using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Surfaces;

/// <summary>
/// The same-origin rule every cookie-carrying surface seam applies (ADR-017 §8.5).
/// <para>
/// A browser attaches cookies to a WebSocket handshake and to a cross-site request
/// regardless of which page initiated it, and no same-origin policy stops it. Where
/// the host honours the surface cookie, it therefore checks the <c>Origin</c> header
/// itself. A request without one is not a browser request, since a user agent that
/// omits <c>Origin</c> omits it on a same-site navigation too, so refusing those
/// would lock out every non-browser client for nothing.
/// </para>
/// </summary>
public static class SurfaceRequestOrigin
{
    /// <summary>
    /// Whether the request's <c>Origin</c> either names the requested host or is absent.
    /// </summary>
    /// <param name="httpContext">The request to check.</param>
    public static bool IsSameOrigin(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var origin = httpContext.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var parsed) &&
               string.Equals(parsed.Host, httpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase);
    }
}
