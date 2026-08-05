using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Surfaces;

/// <summary>
/// Reads a declared credential off the current HTTP request (ADR-017 §4). This is
/// the only place that touches headers and cookies for identity purposes: a provider
/// receives the values of the sources it declared and never the request itself, so it
/// cannot reach the host's own session cookie or an <c>Authorization</c> header it
/// never asked for.
/// </summary>
public sealed class HttpContextSurfaceCredentialReader(IHttpContextAccessor httpContextAccessor)
    : ISurfaceCredentialReader
{
    /// <inheritdoc />
    public string? Read(SurfaceIdentityCredentialKind kind, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || httpContextAccessor.HttpContext is not { } httpContext)
        {
            return null;
        }

        return kind switch
        {
            SurfaceIdentityCredentialKind.Header =>
                httpContext.Request.Headers.TryGetValue(name, out var header) ? header.ToString() : null,
            SurfaceIdentityCredentialKind.Cookie => ReadCookie(httpContext, name),
            _ => null,
        };
    }

    // Header lookup is case-insensitive by definition; the cookie collection is not.
    // The declared-source contract promises case-insensitive matching for both, so
    // the exact hit is tried first and only a miss pays for the scan.
    private static string? ReadCookie(HttpContext httpContext, string name)
    {
        if (httpContext.Request.Cookies.TryGetValue(name, out var exact))
        {
            return exact;
        }

        foreach (var (key, value) in httpContext.Request.Cookies)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }
}
