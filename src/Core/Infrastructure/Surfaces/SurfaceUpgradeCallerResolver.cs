using Callora.Core.Application.Surfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Callora.Core.Infrastructure.Surfaces;

/// <summary>
/// Resolves the surface caller a WebSocket upgrade carries (ADR-017 §9).
/// <para>
/// The origin check is the reason this is not just "read the cookie". A browser sends
/// cookies on a WebSocket handshake to any host, and no same-origin policy prevents
/// it. Without the check, any page on the internet could open a socket that the host
/// would treat as the visitor's own session. A handshake whose <c>Origin</c> names a
/// different host therefore gets no caller at all; the connect is still evaluated by
/// the route's authorizer, it simply carries no surface identity.
/// </para>
/// </summary>
public sealed class SurfaceUpgradeCallerResolver(
    SurfaceSessionCookieAccessor cookies,
    SurfaceSessionAuthenticator authenticator,
    ILogger<SurfaceUpgradeCallerResolver> logger)
{
    /// <summary>Resolves the caller for one upgrade request, or null when it carries none.</summary>
    /// <param name="httpContext">The upgrade request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceCaller?> ResolveAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var cookie = cookies.Read(httpContext);
        if (string.IsNullOrEmpty(cookie))
        {
            return null;
        }

        var host = httpContext.Request.Host.Host;
        if (!IsSameOrigin(httpContext, host))
        {
            logger.LogWarning(
                "Refusing to attach a surface caller to a WebSocket upgrade for {Host}: the Origin header names a different host.",
                host);
            return null;
        }

        return await authenticator.AuthenticateAsync(cookie, host, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsSameOrigin(HttpContext httpContext, string host)
    {
        var origin = httpContext.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            // No Origin means no browser: a user agent cannot omit it on a WebSocket
            // handshake. A client that sends the cookie without one did so on purpose.
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var parsed) &&
               string.Equals(parsed.Host, host, StringComparison.OrdinalIgnoreCase);
    }
}
