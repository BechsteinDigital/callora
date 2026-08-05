using Callora.Core.Application.Surfaces;
using Callora.Core.Infrastructure.Surfaces;
using Microsoft.AspNetCore.Mvc;

namespace Callora.Surface.Rendering.Api;

/// <summary>
/// The two halves of a cross-origin handover (ADR-017 §8.4): the source surface asks
/// for a one-time ticket, the target surface exchanges it for a session of its own.
/// <para>
/// Both routes are anonymous at the platform layer and authorise themselves. Issuing
/// requires a valid surface session on the requesting host and a matching
/// <c>Origin</c>, so another site cannot mint a ticket out of a visitor's cookie.
/// Redeeming requires nothing but the secret, which is why the secret is
/// single-use, short-lived and bound to one host.
/// </para>
/// </summary>
public static class SurfaceHandoffEndpoints
{
    private const string TicketRoute = "/surface/handoff/tickets";
    private const string RedeemRoute = "/surface/handoff/redeem";

    public static IEndpointRouteBuilder MapSurfaceHandoffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(TicketRoute, IssueAsync)
            .AllowAnonymous()
            .WithName("Surfaces_Handoff_Issue")
            .ExcludeFromDescription();

        endpoints.MapGet(RedeemRoute, RedeemAsync)
            .AllowAnonymous()
            .WithName("Surfaces_Handoff_Redeem")
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> IssueAsync(
        SurfaceHandoffTicketApiRequest? request,
        HttpContext httpContext,
        SurfaceSessionAuthenticator authenticator,
        SurfaceSessionCookieAccessor cookies,
        SurfaceHandoffService handoff,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.SurfaceKey))
        {
            return Results.BadRequest();
        }

        var host = httpContext.Request.Host.Host;
        if (!IsSameOrigin(httpContext, host))
        {
            // A cross-site POST would ride the visitor's cookie. It could not read the
            // response, but it could still burn a ticket, so it is refused outright.
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var source = await authenticator
            .AuthenticateAsync(cookies.Read(httpContext), host, cancellationToken)
            .ConfigureAwait(false);
        if (source is null)
        {
            return Results.Unauthorized();
        }

        var issue = await handoff.IssueAsync(source, request.SurfaceKey, cancellationToken).ConfigureAwait(false);
        if (issue.Status != SurfaceHandoffStatus.Ok || issue.Secret is null)
        {
            // One uniform refusal: which surface is misconfigured, and whether the
            // caller was authenticated at all, are host-side facts.
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var redeemUrl = new UriBuilder(httpContext.Request.Scheme, issue.TargetAudience!)
        {
            Path = RedeemRoute,
            Query = $"ticket={Uri.EscapeDataString(issue.Secret)}" +
                    $"&returnPath={Uri.EscapeDataString(SafeReturnPath(request.ReturnPath))}",
        }.Uri.ToString();

        return Results.Ok(new SurfaceHandoffTicketApiResponse(
            redeemUrl, issue.TargetSurfaceKey!, issue.ExpiresAtUtc!.Value));
    }

    private static async Task<IResult> RedeemAsync(
        [FromQuery] string? ticket,
        [FromQuery] string? returnPath,
        HttpContext httpContext,
        SurfaceHandoffService handoff,
        SurfaceSessionService sessions,
        SurfaceSessionCookieAccessor cookies,
        CancellationToken cancellationToken)
    {
        // Redemption is a GET that consumes a one-time ticket, so anything fetching
        // the URL on the visitor's behalf destroys it before they arrive. Only clients
        // that announce themselves are refused: requiring a positive navigation signal
        // instead would lock out every client that sends no Sec-Fetch headers at all.
        if (IsPrefetch(httpContext))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var host = httpContext.Request.Host.Host;
        var redemption = await handoff.RedeemAsync(ticket, host, cancellationToken).ConfigureAwait(false);
        if (redemption.Status != SurfaceHandoffStatus.Ok ||
            redemption.Surface is null ||
            redemption.Caller is null)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        // The target mints its own session, so the identity that arrives by ticket
        // ends up bound to this host exactly like one established here directly.
        var establishment = await sessions
            .EstablishAsync(
                redemption.Surface,
                host,
                cookies.Read(httpContext),
                SurfaceIdentityResolution.Authenticated(redemption.Caller),
                cancellationToken)
            .ConfigureAwait(false);

        cookies.Write(httpContext, establishment);
        return Results.Redirect(SafeReturnPath(returnPath));
    }

    // Only a site-relative path survives. An absolute URL, a protocol-relative "//host"
    // or a backslash would let the ticket issuer choose where the visitor lands.
    private static string SafeReturnPath(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "/";
        }

        var trimmed = candidate.Trim();
        return trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal) &&
               !trimmed.Contains('\\', StringComparison.Ordinal)
            ? trimmed
            : "/";
    }

    // Sec-Purpose is the current spelling, Purpose/X-Purpose the one Chrome and some
    // link scanners still send. A request that says it is speculative is taken at its
    // word; one that says nothing is treated as a real visit.
    private static bool IsPrefetch(HttpContext httpContext)
    {
        foreach (var header in (ReadOnlySpan<string>)["Sec-Purpose", "Purpose", "X-Purpose", "X-Moz"])
        {
            if (httpContext.Request.Headers.TryGetValue(header, out var value) &&
                value.ToString().Contains("prefetch", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSameOrigin(HttpContext httpContext, string host)
    {
        var origin = httpContext.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out var parsed) &&
               string.Equals(parsed.Host, host, StringComparison.OrdinalIgnoreCase);
    }
}
