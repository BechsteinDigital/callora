using System.Net.WebSockets;
using System.Text;

namespace Callora.Plugin.Communication.Infrastructure.Transport;

/// <summary>
/// Reassembles fragmented WebSocket text messages under a hard byte cap and an idle
/// timeout (#108).
/// <para>
/// The naive loop — append every fragment to a growing buffer until
/// <c>EndOfMessage</c> — lets one peer allocate without bound simply by never
/// setting that flag. This reader counts bytes as they arrive and aborts the
/// connection the moment the cap is passed, so the memory a connection can hold is
/// the cap, not the peer's patience.
/// </para>
/// </summary>
internal static class BoundedWebSocketReader
{
    /// <summary>
    /// Reads the next whole text message, or null when the socket closes.
    /// </summary>
    /// <exception cref="WebSocketException">
    /// The peer exceeded <paramref name="maxMessageBytes"/>. The socket is closed
    /// with <see cref="WebSocketCloseStatus.MessageTooBig"/> first, so the peer learns
    /// why rather than seeing an unexplained drop.
    /// </exception>
    public static async ValueTask<string?> ReadTextAsync(
        WebSocket socket,
        byte[] receiveBuffer,
        int maxMessageBytes,
        TimeSpan idleTimeout,
        CancellationToken cancellationToken)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(idleTimeout);

        using var message = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(receiveBuffer, idle.Token).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (message.Length + result.Count > maxMessageBytes)
            {
                await CloseTooBigAsync(socket, maxMessageBytes, cancellationToken).ConfigureAwait(false);
                throw new WebSocketException(
                    WebSocketError.HeaderError,
                    $"The peer exceeded the {maxMessageBytes}-byte message limit.");
            }

            message.Write(receiveBuffer, 0, result.Count);

            // Each fragment restarts the idle window: a slow but live peer is fine,
            // a silent one is not.
            idle.CancelAfter(idleTimeout);
        }
        while (!result.EndOfMessage);

        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
    }

    private static async Task CloseTooBigAsync(WebSocket socket, int maxMessageBytes, CancellationToken cancellationToken)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await socket.CloseOutputAsync(
                    WebSocketCloseStatus.MessageTooBig,
                    $"Messages are limited to {maxMessageBytes} bytes.",
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (WebSocketException)
        {
            // The peer may already be gone; the abort below is what matters.
        }
        catch (OperationCanceledException)
        {
            // Shutting down anyway.
        }
    }
}
