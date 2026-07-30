using Callora.Plugin.Communication.Application.RealtimeMedia;
using CalloraVoipSdk.WebRtc;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// Wraps one SDK <see cref="RemoteTrack"/> as a neutral <see cref="IRemoteMediaTrack"/>: maps the SDK track
/// kind and stream id, and projects each incoming <see cref="EncodedFrame"/> onto a neutral
/// <see cref="MediaFrame"/> (payload, RTP timestamp, key-frame flag, and the track's stream id so a
/// forwarding layer knows the source). The SDK frame payload is valid only for the callback; a forwarding
/// consumer copies it — this adapter forwards it verbatim without copying.
/// </summary>
internal sealed class CalloraVoipSdkRemoteTrack : IRemoteMediaTrack
{
    private readonly RemoteTrack _track;

    public CalloraVoipSdkRemoteTrack(RemoteTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        _track = track;
        Kind = CalloraVoipSdkFrameMapper.ToMediaTrackKind(track.Kind);
        StreamId = track.StreamId;
        _track.FrameReceived += OnFrameReceived;
    }

    /// <inheritdoc />
    public MediaTrackKind Kind { get; }

    /// <inheritdoc />
    public string? StreamId { get; }

    /// <inheritdoc />
    public event EventHandler<MediaFrame>? FrameReceived;

    /// <summary>
    /// Detaches from the SDK track's frame event. Called by the owning <see cref="CalloraVoipSdkMediaPeer"/>
    /// when it tears down, so the inbound subscription is released deterministically with the peer rather than
    /// only when the SDK eventually drops the track (symmetry with the sibling <c>SdkCallAudioStream</c>).
    /// </summary>
    internal void Detach() => _track.FrameReceived -= OnFrameReceived;

    private void OnFrameReceived(object? sender, EncodedFrame frame) =>
        FrameReceived?.Invoke(this, CalloraVoipSdkFrameMapper.ToMediaFrame(frame, StreamId));
}
