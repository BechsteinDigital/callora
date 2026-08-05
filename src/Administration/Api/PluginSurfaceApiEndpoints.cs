using System.Text.Json;
using Callora.Core.Application.Audit;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Infrastructure.Surfaces;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Callora.Administration.Api;

/// <summary>
/// The seam a surface's own visitors call (#125 block B), mounted under the reserved
/// <c>/surface-api/{pluginId}/…</c> prefix on the surface's host.
/// <para>
/// The division of labour is the point. The host answers the questions only it can:
/// is the surface context valid, does it belong to this host, is the plugin available
/// in that workspace, is the route mounted, is the request within its limits, and is
/// the execution recorded. The plugin answers the one only it can: may this subject
/// perform this action. The platform carries claims and interprets none of them.
/// </para>
/// </summary>
public static class PluginSurfaceApiEndpoints
{
    /// <summary>Maps the reserved surface API prefix.</summary>
    /// <param name="endpoints">Route builder to map onto.</param>
    /// <param name="rateLimitingPolicy">
    /// Rate-limiting policy to apply, when the composition registered one. Passed in
    /// rather than named here so a host without the limiter still maps the seam.
    /// </param>
    public static IEndpointRouteBuilder MapPluginSurfaceApiEndpoints(
        this IEndpointRouteBuilder endpoints,
        string? rateLimitingPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var route = endpoints
            .MapMethods(
                $"{SurfaceApiRouteRules.Prefix}/{{pluginId}}/{{**routePath}}",
                ["GET", "POST", "PUT", "DELETE"],
                HandleAsync)
            .AllowAnonymous()
            .WithName("PluginExtensions_SurfaceApi")
            .ExcludeFromDescription();

        if (!string.IsNullOrWhiteSpace(rateLimitingPolicy))
        {
            route.RequireRateLimiting(rateLimitingPolicy);
        }

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string pluginId,
        string? routePath,
        HttpContext httpContext,
        ICalloraPluginCatalog pluginCatalog,
        SurfaceSessionAuthenticator authenticator,
        SurfaceSessionCookieAccessor cookies,
        SurfaceApiOptions options,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Callora.Administration.Api.PluginSurfaceApi");

        // Before anything reads the cookie: a cross-site page must not be able to act
        // through the visitor's surface session (ADR-017 §8.5).
        if (!SurfaceRequestOrigin.IsSameOrigin(httpContext))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var (body, tooLarge) = await CappedRequestBody
            .ReadAsync(httpContext, options.MaxRequestBodyBytes, cancellationToken)
            .ConfigureAwait(false);
        if (tooLarge)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var context = await authenticator
            .AuthenticateAsync(cookies.Read(httpContext), httpContext.Request.Host.Host, cancellationToken)
            .ConfigureAwait(false);
        if (context is null)
        {
            return Results.Unauthorized();
        }

        var inventory = SurfaceApiRouteInventory.Build(
            pluginCatalog.GetExports<IHostSurfaceApiContributor>());
        var match = PluginSurfaceApiRouteMatcher.FindMatch(
            inventory, pluginId, httpContext.Request.Method, routePath ?? string.Empty);
        if (match is null)
        {
            LogRejectionsOnMiss(logger, inventory, pluginId, routePath);
            return Results.NotFound();
        }

        // A guest context is a key for state, never an entitlement. Only a route that
        // opted in sees one; everything else needs a real identity.
        if (match.Mounted.Route.Audience == SurfaceApiRouteAudience.Authenticated &&
            context.Caller is not AuthenticatedSurfaceCaller)
        {
            return Results.Unauthorized();
        }

        if (httpContext.RequestServices.GetService<IPluginAvailabilityEvaluator>() is { } availabilityEvaluator)
        {
            var availability = await availabilityEvaluator
                .EvaluateAsync(match.Mounted.PluginId, context.WorkspaceKey, cancellationToken)
                .ConfigureAwait(false);
            if (!availability.IsAvailable)
            {
                // Deliberately indistinguishable from a route that does not exist: a
                // visitor learning which plugins a workspace lacks learns too much.
                return Results.NotFound();
            }
        }

        var requestId = httpContext.TraceIdentifier;
        var request = new HostSurfaceApiRequest(
            match.Mounted.PluginId,
            httpContext.Request.Method,
            routePath ?? string.Empty,
            match.RouteValues,
            HttpQueryValues.Read(httpContext.Request.Query),
            ParseJson(body),
            requestId,
            context.TenantKey,
            context.WorkspaceKey,
            context.SurfaceKey,
            context.Caller);

        return await DispatchAsync(
                httpContext, match, request, context, options, logger, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> DispatchAsync(
        HttpContext httpContext,
        PluginSurfaceApiRouteMatch match,
        HostSurfaceApiRequest request,
        SurfaceCallerContext context,
        SurfaceApiOptions options,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.HandlerTimeout);

        try
        {
            var response = await match.Mounted.Route.Handler
                .HandleAsync(request, deadline.Token)
                .ConfigureAwait(false);

            await AuditAsync(httpContext, request, context, response.StatusCode, null).ConfigureAwait(false);

            return response.Payload is null
                ? Results.StatusCode(response.StatusCode)
                : Results.Json(response.Payload, statusCode: response.StatusCode);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Surface API handler of plugin {PluginId} exceeded its {Timeout} deadline for {Method} {RoutePath} ({RequestId}).",
                request.PluginId,
                options.HandlerTimeout,
                request.HttpMethod,
                request.RoutePath,
                request.RequestId);
            await AuditAsync(httpContext, request, context, StatusCodes.Status504GatewayTimeout, "timeout")
                .ConfigureAwait(false);
            return Results.StatusCode(StatusCodes.Status504GatewayTimeout);
        }
        catch (Exception ex)
        {
            // A plugin fault is logged with its provenance and answered without detail:
            // the visitor gets a request id, not a stack trace.
            logger.LogError(
                ex,
                "Surface API handler of plugin {PluginId} threw for {Method} {RoutePath} ({RequestId}).",
                request.PluginId,
                request.HttpMethod,
                request.RoutePath,
                request.RequestId);
            await AuditAsync(httpContext, request, context, StatusCodes.Status500InternalServerError, ex.GetType().Name)
                .ConfigureAwait(false);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task AuditAsync(
        HttpContext httpContext,
        HostSurfaceApiRequest request,
        SurfaceCallerContext context,
        int statusCode,
        string? failure)
    {
        if (httpContext.RequestServices.GetService<IHostAuditStore>() is not { } auditStore)
        {
            return;
        }

        // Provenance an operator can act on: which plugin ran, for which workspace and
        // surface, on whose behalf, and under which request id.
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["workspaceKey"] = context.WorkspaceKey,
            ["surfaceKey"] = context.SurfaceKey,
            ["method"] = request.HttpMethod,
            ["routePath"] = request.RoutePath,
            ["requestId"] = request.RequestId,
            ["statusCode"] = statusCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["issuer"] = context.Caller.Subject.Issuer,
        };
        if (failure is not null)
        {
            metadata["failure"] = failure;
        }

        try
        {
            await auditStore.AppendAsync(new HostAuditEntry(
                DateTimeOffset.UtcNow,
                "surface-api.request",
                request.PluginId,
                statusCode < 400,
                context.Caller.Subject.Key,
                $"{request.HttpMethod} {request.RoutePath}",
                metadata)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // An audit sink that is down must not turn a served request into a failed
            // one; the log line above already carries the same provenance.
        }
    }

    private static void LogRejectionsOnMiss(
        ILogger logger,
        SurfaceApiRouteInventory inventory,
        string pluginId,
        string? routePath)
    {
        // Only on a miss, and only when something was actually refused: a route that
        // silently never matches is the hardest misconfiguration to see from outside.
        foreach (var rejection in inventory.Rejections)
        {
            if (string.Equals(rejection.PluginId, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "No surface API route for {PluginId}/{RoutePath}; plugin declared {Method} {Template} but it was refused: {Reason}.",
                    pluginId,
                    routePath,
                    rejection.HttpMethod,
                    rejection.RouteTemplate,
                    rejection.Reason);
            }
        }
    }

    private static JsonElement? ParseJson(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
