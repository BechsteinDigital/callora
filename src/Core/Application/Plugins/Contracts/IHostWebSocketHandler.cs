using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Services one accepted plugin WebSocket. Runs only after the route's
/// <see cref="IWebSocketConnectAuthorizer"/> authorized the connect, so the handler
/// can assume an already-validated caller. The handler owns the read/write loop for
/// the lifetime of the returned task; the host disposes the socket once it completes.
/// </summary>
[CalloraExtensible("Extension point — implement to service an accepted plugin WebSocket")]
public interface IHostWebSocketHandler
{
    /// <summary>
    /// Runs the duplex exchange for the accepted connection.
    /// </summary>
    Task HandleAsync(HostWebSocketConnection connection, CancellationToken cancellationToken = default);
}
