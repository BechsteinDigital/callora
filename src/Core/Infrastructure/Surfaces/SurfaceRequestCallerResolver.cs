using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Microsoft.AspNetCore.Http;

namespace Callora.Core.Infrastructure.Surfaces;

/// <summary>
/// Establishes the caller for one surface request and puts the resulting context
/// cookie on the response (ADR-017 §6, §8). It exists so the render endpoint stays a
/// thin route: identity resolution, session handling and cookie mechanics live here,
/// and the later seams (surface API, WebSocket upgrades) reuse the same path.
/// </summary>
public sealed class SurfaceRequestCallerResolver(
    SurfaceIdentityResolver identityResolver,
    SurfaceSessionService sessions,
    SurfaceSessionCookieAccessor cookies,
    ISurfaceCredentialReader credentials)
{
    /// <summary>
    /// Resolves identity, establishes the session, and writes the cookie when it changed.
    /// </summary>
    /// <param name="httpContext">Current request.</param>
    /// <param name="surface">The resolved surface, carrying its identity assignment.</param>
    /// <param name="locale">Effective surface locale.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SurfaceSessionEstablishment> EstablishAsync(
        HttpContext httpContext,
        WorkspaceSurfaceSnapshot surface,
        string locale,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(surface);

        var descriptor = new SurfaceRequestDescriptor(
            httpContext.Request.Method,
            RelativePath(httpContext, surface),
            locale,
            httpContext.Request.Headers.Origin.ToString() is { Length: > 0 } origin ? origin : null,
            httpContext.Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null);

        var resolution = await identityResolver
            .ResolveAsync(surface, descriptor, credentials, cancellationToken)
            .ConfigureAwait(false);

        var establishment = await sessions
            .EstablishAsync(surface, httpContext.Request.Host.Host, cookies.Read(httpContext), resolution, cancellationToken)
            .ConfigureAwait(false);

        cookies.Write(httpContext, establishment);
        return establishment;
    }

    // A provider reasons about its own surface, not about where the surface is
    // mounted, so the path it sees is relative to the surface's public prefix.
    private static string RelativePath(HttpContext httpContext, WorkspaceSurfaceSnapshot surface)
    {
        var path = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : "/";
        var prefix = surface.PublicPathPrefix;
        if (string.IsNullOrWhiteSpace(prefix) || prefix == "/")
        {
            return path;
        }

        var trimmed = prefix.TrimEnd('/');
        if (!path.StartsWith(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        var relative = path[trimmed.Length..];
        return string.IsNullOrEmpty(relative) ? "/" : relative;
    }
}
