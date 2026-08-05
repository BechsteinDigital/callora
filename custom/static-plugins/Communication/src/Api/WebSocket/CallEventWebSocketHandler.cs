using System.Net.WebSockets;
using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Calls;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Services an accepted call-event socket: subscribes to the workspace the authorizer resolved and
/// writes each transition out as JSON until the client disconnects (#116).
/// </summary>
/// <remarks>
/// Send-only. The client has the Admin API for everything it wants to do to a call, so an inbound
/// frame here would be a second, weaker control path; the handler reads only to notice the close.
/// </remarks>
public sealed class CallEventWebSocketHandler(
    CallEventBroadcaster broadcaster,
    ILogger<CallEventWebSocketHandler> logger) : IHostWebSocketHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Small: the handler never reassembles a message, it only needs to see the close frame.</summary>
    private const int CloseWatchBufferBytes = 256;

    /// <inheritdoc />
    public async Task HandleAsync(HostWebSocketConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var workspaceKey = connection.Subject;
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return;
        }

        using var closed = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var subscription = broadcaster.Subscribe(workspaceKey);

        // Watching for the close frame is what makes an abandoned socket release its subscription:
        // without a read in flight, a client that goes away is only noticed on the next send, which
        // for a quiet workspace may be hours later.
        var watchClose = WatchForCloseAsync(connection.Socket, closed);

        try
        {
            await foreach (var notification in subscription.Events.ReadAllAsync(closed.Token).ConfigureAwait(false))
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(notification, SerializerOptions);
                await connection.Socket
                    .SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, closed.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // The client disconnected or the host is shutting down.
        }
        catch (WebSocketException ex)
        {
            logger.LogDebug(ex, "Call event socket for workspace {WorkspaceKey} ended.", workspaceKey);
        }
        finally
        {
            await closed.CancelAsync().ConfigureAwait(false);
            await watchClose.ConfigureAwait(false);
        }
    }

    private static async Task WatchForCloseAsync(System.Net.WebSockets.WebSocket socket, CancellationTokenSource closed)
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
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            // Any of these mean the same thing here: the socket is gone.
        }
        finally
        {
            await closed.CancelAsync().ConfigureAwait(false);
        }
    }
}
