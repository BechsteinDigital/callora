namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// One encoded media frame crossing the provider port — the neutral counterpart to the SDK's encoded
/// frame. Transport-only: <see cref="Payload"/> is the raw depacketised codec bitstream (the app owns no
/// codec; the browser decodes). Frames flow in on <see cref="IRemoteMediaTrack.FrameReceived"/> and out
/// through <see cref="IMediaOutboundTrack.SendFrameAsync"/>, carrying the source RTP timestamp 1:1 so a
/// forwarding layer preserves A/V sync.
/// </summary>
/// <param name="Payload">The encoded codec payload. On a received frame it is valid only for the duration
/// of the <see cref="IRemoteMediaTrack.FrameReceived"/> callback; a forwarding consumer must copy it.</param>
/// <param name="RtpTimestamp">The frame's RTP timestamp when known — present for video, <see langword="null"/>
/// for audio whose inbound path does not surface one.</param>
/// <param name="IsKeyFrame">Whether this is a key/intra frame; always <see langword="false"/> for audio.</param>
/// <param name="StreamId">The MediaStream id (a=msid) the frame belongs to — the source participant for
/// SFU forwarding — or <see langword="null"/> when the remote advertised none.</param>
internal readonly record struct MediaFrame(
    ReadOnlyMemory<byte> Payload,
    uint? RtpTimestamp,
    bool IsKeyFrame,
    string? StreamId);
