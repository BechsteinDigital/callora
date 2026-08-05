using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Validates the <c>connectToken</c> route value against an outstanding call-event ticket before the
/// host accepts the socket, and hands the resolved workspace to the handler as the connection subject.
/// Denials are uniform, so an unknown, expired and already-used token are indistinguishable to the
/// caller.
/// </summary>
public sealed class CallEventConnectTokenAuthorizer(CallEventTicketStore tickets) : IWebSocketConnectAuthorizer
{
    /// <summary>Route-value name carrying the connect token (matches <c>calls/{connectToken}</c>).</summary>
    public const string ConnectTokenRouteValue = "connectToken";

    /// <inheritdoc />
    public ValueTask<WebSocketConnectAuthorization> AuthorizeAsync(
        HostWebSocketConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.RouteValues.TryGetValue(ConnectTokenRouteValue, out var token) || string.IsNullOrWhiteSpace(token))
        {
            return ValueTask.FromResult(WebSocketConnectAuthorization.Deny("missing connect token"));
        }

        var workspaceKey = tickets.TryConsume(token);
        return ValueTask.FromResult(string.IsNullOrEmpty(workspaceKey)
            ? WebSocketConnectAuthorization.Deny("invalid connect token")
            : WebSocketConnectAuthorization.Allow(workspaceKey));
    }
}
