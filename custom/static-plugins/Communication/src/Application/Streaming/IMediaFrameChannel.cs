using Callora.Plugin.Communication.Application.Streaming.Protocol;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Transport-neutral duplex channel of <see cref="MediaStreamMessage"/> frames. The
/// <see cref="MediaBridge"/> talks to this rather than to a concrete WebSocket, so the bridge
/// logic is testable without a real socket and the WS framing lives in the transport adapter.
/// </summary>
public interface IMediaFrameChannel
{
    /// <summary>Sends one frame to the consumer.</summary>
    ValueTask SendAsync(MediaStreamMessage message, CancellationToken cancellationToken = default);

    /// <summary>Receives the next frame from the consumer, or <see langword="null"/> once the channel closes.</summary>
    ValueTask<MediaStreamMessage?> ReceiveAsync(CancellationToken cancellationToken = default);
}
