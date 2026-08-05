using System.Net.WebSockets;
using System.Text;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Infrastructure.Transport;

namespace Callora.Plugin.Communication.Api.WebSocket;

using WebSocket = System.Net.WebSockets.WebSocket;

/// <summary>
/// A thin duplex JSON-frame channel over a raw <see cref="WebSocket"/> for the WebRTC signalling
/// protocol: it serializes <see cref="WebRtcSignalMessage"/> to text frames and reassembles whole text
/// messages on receive. Sends are serialized behind a lock because a peer's local ICE candidates trickle
/// out concurrently with the read loop and the socket's send is not safe for concurrent
/// callers. Malformed or unknown text is surfaced to the caller (as a <see langword="null"/> parse) rather
/// than ending the stream; <see cref="ReceiveAsync"/> returns <see langword="null"/> only on socket close.
/// </summary>
internal sealed class WebRtcSignalingChannel(WebSocket socket) : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly byte[] _receiveBuffer = new byte[8192];

    /// <summary>Serializes and sends one signalling frame as a UTF-8 text message.</summary>
    public async ValueTask SendAsync(WebRtcSignalMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var bytes = Encoding.UTF8.GetBytes(message.ToJson());

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Reads the next whole text message and returns its raw JSON, or <see langword="null"/> when the
    /// socket closes. Fragmented frames are reassembled under a hard byte cap and an idle timeout
    /// (#108); parsing/validation is the caller's concern so a single malformed frame can be logged
    /// and ignored without ending the stream.
    /// </summary>
    public ValueTask<string?> ReceiveTextAsync(CancellationToken cancellationToken = default) =>
        BoundedWebSocketReader.ReadTextAsync(
            socket,
            _receiveBuffer,
            CommunicationStreamLimits.MaxSignalingMessageBytes,
            CommunicationStreamLimits.IdleTimeout,
            cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _sendLock.Dispose();
}
