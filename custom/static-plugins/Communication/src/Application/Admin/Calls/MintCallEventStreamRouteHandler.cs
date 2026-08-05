using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Api.WebSocket;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>POST calls/event-stream</c> — mints the ticket a client redeems to follow the
/// workspace's call transitions live (#116).
/// </summary>
/// <remarks>
/// A browser cannot put an Authorization header on a WebSocket handshake, which is why the stream is
/// reached through a ticket rather than the bearer token: the authorization happens here, on a normal
/// authenticated request, and the socket carries only its short-lived, single-use result.
/// </remarks>
public sealed class MintCallEventStreamRouteHandler(CallEventTicketStore tickets) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CallAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return ValueTask.FromResult(scopeError!);
        }

        var token = tickets.Mint(workspaceKey);
        return ValueTask.FromResult(new HostAdminApiResponse(
            201, CallEventStreamTicketView.For(token, tickets.TicketTimeToLive)));
    }
}
