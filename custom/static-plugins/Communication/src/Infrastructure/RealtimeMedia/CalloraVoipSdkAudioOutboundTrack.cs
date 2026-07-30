using Callora.Plugin.Communication.Application.RealtimeMedia;
using CalloraVoipSdk.WebRtc;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// Wraps one SDK <see cref="IAudioTrack"/> as a neutral <see cref="IMediaOutboundTrack"/>: a neutral
/// <see cref="MediaFrame"/> is sent as an encoded audio frame, its RTP timestamp stamped on the outbound
/// packets (0 when the frame carries none — the audio inbound path does not surface one).
/// </summary>
internal sealed class CalloraVoipSdkAudioOutboundTrack : IMediaOutboundTrack
{
    private readonly IAudioTrack _track;

    public CalloraVoipSdkAudioOutboundTrack(IAudioTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        _track = track;
    }

    /// <inheritdoc />
    public Task SendFrameAsync(MediaFrame frame, CancellationToken ct = default) =>
        _track.SendFrameAsync(frame.Payload, frame.RtpTimestamp ?? 0, ct);
}
