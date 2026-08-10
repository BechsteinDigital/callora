using System.Text;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Callora.Administration.Api;

/// <summary>
/// Maps the platform's reserved <c>/public/{pluginId}/{**routePath}</c> prefix onto
/// plugin public HTTP endpoints. These routes are <strong>anonymous</strong> at the
/// cookie/JWT layer — the host enforces no authentication — so each handler is
/// entirely responsible for input validation and any access control it requires
/// (for example: verifying a signed invitation token or a webhook HMAC).
/// <para>
/// Both GET and POST are accepted. Unrecognized plugin/method/path combinations
/// return 404 without a body so that no routing topology is leaked. Request bodies
/// are limited to 1 MB; larger payloads are rejected with 413 before the handler
/// is invoked. Handler exceptions are caught, logged server-side, and returned as
/// 500 without detail.
/// </para>
/// </summary>
public static class PluginPublicHttpEndpoints
{
    private const int BodySizeLimitBytes = 1 * 1024 * 1024; // 1 MB

    // Curated allowlist of request headers forwarded to plugin handlers. Cookie and
    // Authorization are deliberately excluded so a user's host session never leaks to
    // a plugin on this anonymous public endpoint. Handlers that need caller identity
    // must carry it in the route/body (for example: a signed token), not in a header.
    private static readonly HashSet<string> ForwardableHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type",
        "Accept",
        "Accept-Language",
        "User-Agent",
        "X-Forwarded-For",
        "X-Real-IP",
    };

    public static IEndpointRouteBuilder MapPluginPublicHttpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/public/{pluginId}/{**routePath}", HandlePublicHttpAsync)
            .AllowAnonymous()
            .WithName("PluginExtensions_PublicHttp_Get")
            .ExcludeFromDescription();

        endpoints.MapPost("/public/{pluginId}/{**routePath}", HandlePublicHttpAsync)
            .AllowAnonymous()
            .WithName("PluginExtensions_PublicHttp_Post")
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task HandlePublicHttpAsync(
        string pluginId,
        string? routePath,
        HttpContext httpContext,
        ICalloraPluginCatalog pluginCatalog,
        ILoggerFactory loggerFactory,
        // Optional: Ein Host ohne Fehlerbudget rechnet nichts zu und verhält sich unverändert.
        [FromServices] PluginFaultRegistry? faults,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Callora.Administration.Api.PluginPublicHttpEndpoints");
        var method = httpContext.Request.Method;

        // Hard byte-cap the body read regardless of ContentLength / Transfer-Encoding.
        // Runs before the matcher/handler so an oversized payload never reaches plugin code.
        var (body, tooLarge) = await CappedRequestBody
            .ReadAsync(httpContext, BodySizeLimitBytes, cancellationToken)
            .ConfigureAwait(false);
        if (tooLarge)
        {
            httpContext.Response.StatusCode = StatusCodes.Status413RequestEntityTooLarge;
            return;
        }

        var contributors = pluginCatalog.GetExports<IHostPublicHttpEndpointContributor>();
        var match = PluginPublicHttpRouteMatcher.FindMatch(contributors, pluginId, method, routePath ?? string.Empty);

        if (match is null)
        {
            // Uniform 404 — no body, no info leak about which routes exist.
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var request = new HostPublicHttpRequest(
            PluginId: match.Contributor.PluginId,
            Method: method,
            RoutePath: routePath ?? string.Empty,
            RouteValues: FlattenRouteValues(match.RouteValues),
            Query: FlattenQuery(httpContext.Request.Query),
            Headers: FlattenHeaders(httpContext.Request.Headers),
            Body: body);

        HostPublicHttpResponse response;
        try
        {
            response = await match.Route.Handler
                .HandleAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled exception in public HTTP handler for plugin {PluginId}, route {RoutePath}",
                pluginId, routePath);

            faults?.Record(pluginId, PluginFaultOrigin.HttpRoute);
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        httpContext.Response.StatusCode = response.StatusCode;
        httpContext.Response.ContentType = response.ContentType;

        if (response.Headers is not null)
        {
            foreach (var (name, value) in response.Headers)
            {
                httpContext.Response.Headers[name] = value;
            }
        }

        if (!string.IsNullOrEmpty(response.Body))
        {
            await httpContext.Response.WriteAsync(response.Body, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads a request body into memory with a hard byte-cap. Returns
    /// <c>(null, true)</c> the moment the accumulated size would exceed
    /// <paramref name="limit"/> — reading stops immediately, so an oversized or
    /// chunked payload never fully materialises. Returns <c>(null, false)</c> for an
    /// empty body (for example: a GET request), otherwise the UTF-8 decoded body.
    /// </summary>
    private static IReadOnlyDictionary<string, string?> FlattenRouteValues(
        IReadOnlyDictionary<string, string> routeValues)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in routeValues)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> FlattenQuery(IQueryCollection query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in query)
        {
            // Take the first value when a key appears multiple times.
            if (!result.ContainsKey(pair.Key))
            {
                result[pair.Key] = pair.Value.Count > 0 ? pair.Value[0] ?? string.Empty : string.Empty;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> FlattenHeaders(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in headers)
        {
            // Forward only curated headers; skip Cookie/Authorization and anything else
            // that could leak the caller's host session onto this anonymous endpoint.
            if (!ForwardableHeaders.Contains(pair.Key))
            {
                continue;
            }

            result[pair.Key] = pair.Value.Count > 0 ? pair.Value[0] ?? string.Empty : string.Empty;
        }

        return result;
    }
}
