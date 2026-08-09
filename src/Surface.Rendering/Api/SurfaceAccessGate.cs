using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Microsoft.AspNetCore.Http;

namespace Callora.Surface.Rendering.Api;

/// <summary>
/// Turns the established caller into a serve-or-refuse decision for the surface's
/// access mode (ADR-014 §6.1, ADR-017 §6.1). This is the authoritative boundary;
/// hiding UI on the client is never a substitute for it.
/// </summary>
public static class SurfaceAccessGate
{
    /// <summary>
    /// Returns the response that refuses the request, or null when it may be served.
    /// </summary>
    /// <param name="surface">The resolved surface.</param>
    /// <param name="establishment">Caller established for this request.</param>
    /// <param name="httpContext">Current request, for building the login redirect.</param>
    public static IResult? Reject(
        WorkspaceSurfaceSnapshot surface,
        SurfaceSessionEstablishment establishment,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(establishment);
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!surface.Authentication.RequiresSignIn())
        {
            return null;
        }

        // The surface has an identity provider it cannot consult right now. Serving
        // anonymously would widen access, so it is closed — a defined error rather
        // than a silent downgrade. The reason stays in the log: telling the visitor
        // which provider is broken helps only an attacker.
        if (establishment.IsClosed)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (establishment.Caller is AuthenticatedSurfaceCaller)
        {
            return null;
        }

        // Nobody signed in, and the surface demands it. Which login applies is the surface's
        // declared choice — before ADR-023 this branched on whether an identity plugin happened
        // to be assigned, which meant the operator never chose it and could not see it.
        return surface.Authentication == SurfaceAuthentication.Administration
            ? LoginRedirect(surface, httpContext)
            : Results.Unauthorized();
    }

    /// <summary>Redirects to the host login, preserving where the visitor wanted to go.</summary>
    /// <param name="surface">The resolved surface.</param>
    /// <param name="httpContext">Current request.</param>
    public static IResult LoginRedirect(WorkspaceSurfaceSnapshot surface, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(httpContext);

        var returnUrl = httpContext.Request.Path + httpContext.Request.QueryString;
        return Results.Redirect(
            $"/login?workspaceKey={Uri.EscapeDataString(surface.WorkspaceKey)}" +
            $"&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }
}
