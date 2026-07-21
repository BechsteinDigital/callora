using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Administration.Api;

/// <summary>
/// Maps the platform's reserved <c>/ws/{pluginId}/{routePath}</c> prefix onto plugin
/// WebSocket endpoints. The upgrade is gated by the matched route's
/// <see cref="IWebSocketConnectAuthorizer"/>: the host validates the connect
/// <em>before</em> accepting the socket, so an unauthorized caller never obtains an
/// upgrade. These endpoints are anonymous at the cookie/JWT layer by design — the
/// consumers are out-of-process agents connecting with a connect-token, not browser
/// sessions — so authorization is delegated entirely to the per-route authorizer.
/// </summary>
public static class PluginWebSocketEndpoints
{
    public static IEndpointRouteBuilder MapPluginWebSocketEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/ws/{pluginId}/{**routePath}", HandlePluginWebSocketAsync)
            .AllowAnonymous()
            .WithName("PluginExtensions_WebSocket")
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task HandlePluginWebSocketAsync(
        string pluginId,
        string? routePath,
        HttpContext httpContext,
        ICalloraPluginCatalog pluginCatalog,
        CancellationToken cancellationToken)
    {
        if (!httpContext.WebSockets.IsWebSocketRequest)
        {
            // Reserved prefix, but not an upgrade: a plain GET has no meaning here.
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var contributors = pluginCatalog.GetExports<IHostWebSocketEndpointContributor>();
        var match = PluginWebSocketRouteMatcher.FindMatch(contributors, pluginId, routePath ?? string.Empty);
        if (match is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var connectRequest = new HostWebSocketConnectRequest(
            match.Contributor.PluginId,
            routePath ?? string.Empty,
            match.RouteValues,
            HttpQueryValues.Read(httpContext.Request.Query),
            httpContext.WebSockets.WebSocketRequestedProtocols.ToArray());

        var authorization = await match.Route.Authorizer
            .AuthorizeAsync(connectRequest, cancellationToken)
            .ConfigureAwait(false);

        if (authorization is null || !authorization.IsAuthorized)
        {
            // Uniform rejection BEFORE any upgrade — the reason stays host-side.
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var socket = await httpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        var connection = new HostWebSocketConnection(socket, connectRequest, authorization.Subject);
        await match.Route.Handler.HandleAsync(connection, cancellationToken).ConfigureAwait(false);
    }
}
