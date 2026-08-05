using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Admin.Calls;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Domain.Streaming;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Admin.Streaming;

/// <summary>
/// Handles <c>POST calls/{callId}/media-streams</c> — mints the one-time ticket a consumer redeems to
/// stream a live call's audio (#114). This is the legitimate way onto the media socket; without it the
/// endpoint exists but nothing can reach it.
/// </summary>
/// <remarks>
/// Three checks stand between a caller and a stream, and the host performs only the first. It resolves
/// the workspace and confirms the permission; this handler validates the requested direction, and the
/// minter verifies the call is one that workspace is running right now. A call the caller does not own
/// answers 404, exactly as a call that never existed — an operator gets no way to probe another
/// workspace's call ids.
/// </remarks>
public sealed class MintMediaStreamRouteHandler(
    IMediaStreamSessionMinter minter,
    ILogger<MintMediaStreamRouteHandler> logger) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CallAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return scopeError!;
        }

        if (!request.RouteValues.TryGetValue("callId", out var callId) || string.IsNullOrWhiteSpace(callId))
        {
            return new HostAdminApiResponse(400, new { error = "callId is required." });
        }

        MintMediaStreamApiRequest? body;
        try
        {
            body = request.Body?.Deserialize<MintMediaStreamApiRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        var consumerRef = body?.ConsumerRef?.Trim();
        if (string.IsNullOrWhiteSpace(consumerRef))
        {
            return new HostAdminApiResponse(400, new { error = "consumerRef is required." });
        }

        if (!TryParseDirection(body?.Direction, out var direction))
        {
            return new HostAdminApiResponse(400, new
            {
                error = "direction must be one of inbound, outbound or bidirectional.",
            });
        }

        var ticket = await minter
            .MintAsync(new MintMediaStreamCommand(workspaceKey, callId, consumerRef, direction), cancellationToken)
            .ConfigureAwait(false);
        if (ticket is null)
        {
            return new HostAdminApiResponse(404, new { error = "Call not found." });
        }

        // Audit trail for a credential that opens a live conversation. The token is deliberately
        // absent: the session id is what correlates this line with the socket that redeems it, and it
        // is not a credential (#114).
        logger.LogInformation(
            "Minted media stream session {SessionId} for call {CallId} in workspace {WorkspaceKey} ({Direction}) for consumer {ConsumerRef} by user {UserId}.",
            ticket.SessionId,
            ticket.CallId,
            workspaceKey,
            ticket.Direction,
            consumerRef,
            request.UserId ?? "unknown");

        return new HostAdminApiResponse(201, MediaStreamTicketView.From(ticket));
    }

    // Absent means duplex — the common case for a voice agent, and the direction the protocol was
    // designed around. Anything unrecognized is rejected rather than coerced: silently widening a
    // listen-only request into duplex would hand out more access than was asked for.
    private static bool TryParseDirection(string? raw, out MediaStreamDirection direction)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            direction = MediaStreamDirection.Bidirectional;
            return true;
        }

        return Enum.TryParse(raw.Trim(), ignoreCase: true, out direction) && Enum.IsDefined(direction);
    }
}
