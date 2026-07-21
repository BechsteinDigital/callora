using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Validates a WebSocket connect request before the socket is accepted. The host
/// owns the accept/reject decision and runs this gate uniformly for every route —
/// the plugin only decides whether a given connect-token is valid. Returning a
/// denied <see cref="WebSocketConnectAuthorization"/> aborts the handshake before
/// any upgrade occurs, so an unauthorized caller never obtains a socket.
/// </summary>
[CalloraExtensible("Extension point — implement to validate a plugin WebSocket connect-token before accept")]
public interface IWebSocketConnectAuthorizer
{
    /// <summary>
    /// Decides whether the connect request may be upgraded to a WebSocket.
    /// </summary>
    ValueTask<WebSocketConnectAuthorization> AuthorizeAsync(
        HostWebSocketConnectRequest request,
        CancellationToken cancellationToken = default);
}
