using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Infrastructure.Surfaces;
using Microsoft.AspNetCore.Mvc;

namespace Callora.Administration.Api;

/// <summary>
/// Maps the platform's reserved <c>/ws/{pluginId}/{routePath}</c> prefix onto plugin
/// WebSocket endpoints. The upgrade is gated by the matched route's
/// <see cref="IWebSocketConnectAuthorizer"/>: the host validates the connect
/// <em>before</em> accepting the socket, so an unauthorized caller never obtains an
/// upgrade. These endpoints are anonymous at the cookie/JWT layer by design — the
/// consumers are out-of-process agents connecting with a connect-token, not browser
/// sessions — so authorization is delegated entirely to the per-route authorizer.
/// <para>
/// A connect that does arrive from a surface carries its caller into the authorizer
/// and the handler (ADR-017 §9). The host attaches it only when the handshake's
/// <c>Origin</c> matches the requested host; the caller is offered as a credential,
/// never as a grant, so the route's authorizer still decides.
/// </para>
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
        // Composed with the surface identity subsystem; a host without it upgrades
        // exactly as before, with no caller on the connect request.
        [FromServices] SurfaceUpgradeCallerResolver? callerResolver,
        // Optional: Ein Host ohne Fehlerbudget rechnet nichts zu und verhält sich unverändert.
        [FromServices] PluginFaultRegistry? faults,
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

        var caller = callerResolver is null
            ? null
            : await callerResolver.ResolveAsync(httpContext, cancellationToken).ConfigureAwait(false);

        var connectRequest = new HostWebSocketConnectRequest(
            match.Contributor.PluginId,
            routePath ?? string.Empty,
            match.RouteValues,
            HttpQueryValues.Read(httpContext.Request.Query),
            httpContext.WebSockets.WebSocketRequestedProtocols.ToArray(),
            caller);

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
        try
        {
            await match.Route.Handler.HandleAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Der Aufrufer geht — oder der Host fährt herunter. Das ist das normale Ende einer
            // langlebigen Verbindung und kein Fehler des Plugins.
            throw;
        }
        catch (Exception)
        {
            // Zurechnen und weiterwerfen: Die Behandlung bleibt, wo sie war (die Pipeline), nur
            // die Urheberschaft ist jetzt festgehalten. Eine geworfene WebSocket-Schleife wiegt
            // schwerer als eine gescheiterte Anfrage — sie nimmt eine bestehende Verbindung mit,
            // und der Client sieht einen Abbruch ohne Statuscode, den er als Netzproblem deutet.
            faults?.Record(pluginId, PluginFaultOrigin.Realtime);
            throw;
        }
    }
}
