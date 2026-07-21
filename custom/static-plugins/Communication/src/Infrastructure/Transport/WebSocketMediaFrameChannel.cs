using System.Net.WebSockets;
using System.Text;
using Callora.Plugin.Communication.Application.Streaming;
using Callora.Plugin.Communication.Application.Streaming.Protocol;

namespace Callora.Plugin.Communication.Infrastructure.Transport;

/// <summary>
/// <see cref="IMediaFrameChannel"/> over a raw <see cref="WebSocket"/>: encodes/decodes frames
/// with <see cref="MediaStreamMessageCodec"/> and reads whole text messages (reassembling
/// fragments). Sends are serialized — <c>WebSocket.SendAsync</c> is not safe for concurrent
/// callers, and the bridge sends from two pumps at once.
/// </summary>
public sealed class WebSocketMediaFrameChannel(WebSocket socket) : IMediaFrameChannel, IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly byte[] _receiveBuffer = new byte[4096];

    /// <inheritdoc />
    public async ValueTask SendAsync(MediaStreamMessage message, CancellationToken cancellationToken = default)
    {
        var bytes = Encoding.UTF8.GetBytes(MediaStreamMessageCodec.Encode(message));

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

    /// <inheritdoc />
    public async ValueTask<MediaStreamMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        // Loop so a single malformed/unknown frame is skipped rather than ending the stream;
        // null is returned only when the socket actually closes.
        while (true)
        {
            using var message = new MemoryStream();

            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(_receiveBuffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                message.Write(_receiveBuffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            var decoded = MediaStreamMessageCodec.TryDecode(json);
            if (decoded is not null)
            {
                return decoded;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _sendLock.Dispose();
}
