using Callora.Plugin.Communication.Application.RealtimeMedia;
using CalloraVoipSdk.WebRtc;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// Wraps one SDK <see cref="IVideoTrack"/> as a neutral <see cref="IMediaOutboundTrack"/>: a neutral
/// <see cref="MediaFrame"/> is packetised and sent as an encoded video frame, its RTP timestamp stamped on
/// the outbound packets (0 when the frame carries none).
/// </summary>
internal sealed class CalloraVoipSdkVideoOutboundTrack : IMediaOutboundTrack
{
    private readonly IVideoTrack _track;

    public CalloraVoipSdkVideoOutboundTrack(IVideoTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        _track = track;
    }

    /// <inheritdoc />
    public Task SendFrameAsync(MediaFrame frame, CancellationToken ct = default) =>
        _track.SendFrameAsync(frame.Payload, frame.RtpTimestamp ?? 0, ct);
}
