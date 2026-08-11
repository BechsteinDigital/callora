using System.Net.WebSockets;
using System.Text.Json;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Callora.Surface.Rendering.Api.SurfaceContext;

/// <summary>
/// The realtime bridge: one WebSocket per open surface, over which the host pushes context
/// values into the browser's local context channel (design §5.3).
/// <para>
/// It is what lets a block declare <c>requires: ['communication.active-call/v1']</c> and update
/// when a call arrives, without anyone writing a socket, a reconnect or a message format. The
/// block code is the same whether the value came from another island in the same tab or from
/// the server.
/// </para>
/// <para>
/// <b>Send-only.</b> A frame arriving from the browser is ignored; the socket is read only to
/// notice the close. Everything in a tab is visible to DevTools and to every script on the page,
/// so a value published from there would carry no authority — a client that wants to change
/// something uses the API for it.
/// </para>
/// <para>
/// <b>The surface is resolved server-side</b> from the host and the path the client names, then
/// gated like a render (ADR-017 §6.1). Naming a surface the caller may not open ends the request
/// rather than the socket.
/// </para>
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("surface/context")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class SurfaceContextController : ControllerBase
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Small: the loop never reassembles an inbound message, it only sees the close.</summary>
    private const int CloseWatchBufferBytes = 256;

    /// <summary>Opens the bridge for the surface at <paramref name="path"/> on this host.</summary>
    [HttpGet]
    public async Task<IActionResult> Connect(
        [FromServices] IWorkspaceManagementStore workspaceStore,
        [FromServices] SurfaceContextBroadcaster broadcaster,
        [FromServices] ILoggerFactory loggerFactory,
        // Composed with the surface identity subsystem. Without it a connection is anonymous,
        // which means surface-wide values and no subject-scoped ones — the safe half, not none.
        [FromServices] SurfaceUpgradeCallerResolver? callerResolver,
        // Composed with the identity subsystem too. Without it a connection is never re-checked,
        // which is correct: there is no session behind it to lose.
        [FromServices] SurfaceContextRevalidator revalidator,
        [FromServices] SurfaceSessionCookieAccessor? cookies,
        [FromQuery] string? path,
        CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            return BadRequest();
        }

        var surface = await workspaceStore
            .ResolveSurfaceByPublicRouteAsync(
                HttpContext.Request.Host.Host, NormalizePath(path), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (surface is null)
        {
            return NotFound();
        }

        var callerContext = callerResolver is null
            ? null
            : await callerResolver.ResolveAsync(HttpContext, cancellationToken).ConfigureAwait(false);

        // Die Sitzung gilt für EINE Fläche. Ohne diesen Vergleich zählte eine auf Fläche A
        // ausgestellte Sitzung auch am Socket der Fläche B als Anmeldung — und der Socket
        // überträgt anschließend den Kontext von B. Der Renderpfad löst den Aufrufer aus der
        // Route auf und kann gar nicht abweichen; dieser Seam liest ihn aus dem Cookie und muss
        // deshalb selbst fragen (ADR-017 §9).
        var caller = SurfaceSessionScope.Matches(callerContext, surface.WorkspaceKey, surface.SurfaceKey)
            ? callerContext!.Caller
            : null;

        // An authenticated surface without an established caller gets no socket. Same rule as the
        // render path: a bridge that upgraded where a page would have redirected to login would be
        // a way around the gate.
        if (surface.Authentication.RequiresSignIn() &&
            caller is not AuthenticatedSurfaceCaller)
        {
            return Unauthorized();
        }

        // Dieselbe Sichtbarkeitsprüfung wie im Renderpfad (ADR-019 §4) — und aus demselben Grund
        // 404 statt 403. Ohne sie war der Socket die Umgehung: Wem die Seite mit 404 antwortete,
        // der bekam hier ein Abo auf denselben Knoten und damit jeden Wert, dessen Adresse kein
        // Subjekt nennt (SurfaceContextAddress.Covers). Der Access Mode allein deckt das nicht ab —
        // eine Fläche kann öffentlich sein und trotzdem einen Claim verlangen.
        if (!SurfaceVisibility.IsReachableBy(
                surface.RequiredClaims,
                surface.GrantedClaims,
                caller,
                identityAvailable: callerResolver is not null))
        {
            return NotFound();
        }

        await PumpAsync(
                surface,
                caller,
                broadcaster,
                loggerFactory,
                revalidator,
                cookies?.Read(HttpContext),
                cancellationToken)
            .ConfigureAwait(false);

        // The socket carried the response; anything else here would try to write a body onto it.
        return new EmptyResult();
    }

    private async Task PumpAsync(
        WorkspaceSurfaceSnapshot surface,
        SurfaceCaller? caller,
        SurfaceContextBroadcaster broadcaster,
        ILoggerFactory loggerFactory,
        SurfaceContextRevalidator revalidator,
        string? cookieValue,
        CancellationToken cancellationToken)
    {
        using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        using var subscription = broadcaster.Subscribe(
            surface.WorkspaceKey,
            surface.SurfaceKey,
            caller?.Subject.Issuer,
            caller?.Subject.SubjectId);

        var logger = loggerFactory.CreateLogger<SurfaceContextController>();
        using var closed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Watching for the close frame is what releases an abandoned subscription: without a read
        // in flight, a tab that goes away is only noticed on the next send — which for a quiet
        // surface may be hours later.
        var watchClose = WatchForCloseAsync(socket, closed);

        // A socket outlives the permission behind it. This ends the connection as soon as the
        // session stops holding — a signed-out visitor keeps receiving context otherwise, for as
        // long as they leave the tab open.
        var watchSession = revalidator.WatchAsync(
                cookieValue,
                HttpContext.Request.Host.Host,
                caller?.Subject.SubjectId,
                closed);

        try
        {
            await foreach (var message in subscription.Messages.ReadAllAsync(closed.Token).ConfigureAwait(false))
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
                await socket
                    .SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, closed.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The tab closed or the host is shutting down.
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "Surface context bridge closed for {Surface}.", surface.SurfaceKey);
        }
        finally
        {
            await closed.CancelAsync().ConfigureAwait(false);
            await watchClose.ConfigureAwait(false);
            await watchSession.ConfigureAwait(false);
        }
    }

    // The client names the path it is rendered at; the surface is still resolved from it
    // server-side, so this is a lookup key, not a claim.
    private static string NormalizePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? "/" : path.StartsWith('/') ? path : "/" + path;

    private static async Task WatchForCloseAsync(WebSocket socket, CancellationTokenSource closed)
    {
        var buffer = new byte[CloseWatchBufferBytes];
        try
        {
            while (!closed.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, closed.Token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // Anything else is ignored on purpose: this direction carries no authority.
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
            // The peer vanished without a close frame.
        }
        finally
        {
            await closed.CancelAsync().ConfigureAwait(false);
        }
    }
}
