using Callora.Plugin.Communication.Application.RealtimeMedia;
using CalloraVoipSdk.WebRtc;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// Pure translation between the SDK's <see cref="EncodedFrame"/> / <see cref="TrackKind"/> and the neutral
/// port types (<see cref="MediaFrame"/> / <see cref="MediaTrackKind"/>). Kept a static seam so the
/// field-by-field mapping is verifiable without materialising an SDK <see cref="RemoteTrack"/> (whose event
/// source is not constructible from tests).
/// </summary>
internal static class CalloraVoipSdkFrameMapper
{
    /// <summary>Projects one received SDK frame onto a neutral <see cref="MediaFrame"/>, carrying the track's
    /// <paramref name="streamId"/> so a forwarding layer knows the source. The payload is forwarded verbatim
    /// (the SDK buffer is valid only during the callback; a forwarding consumer copies it).</summary>
    public static MediaFrame ToMediaFrame(EncodedFrame frame, string? streamId) =>
        new(frame.Payload, frame.RtpTimestamp, frame.IsKeyFrame, streamId);

    /// <summary>Maps the SDK track kind onto the neutral kind.</summary>
    public static MediaTrackKind ToMediaTrackKind(TrackKind kind) => kind switch
    {
        TrackKind.Audio => MediaTrackKind.Audio,
        TrackKind.Video => MediaTrackKind.Video,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown SDK track kind."),
    };
}
