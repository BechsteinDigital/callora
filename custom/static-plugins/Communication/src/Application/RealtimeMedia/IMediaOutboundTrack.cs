namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// A send-only outbound track added to an <see cref="IMediaPeer"/> (one m-line on the shared transport) —
/// the neutral counterpart to the SDK's per-track send handle. A forwarding layer sends one already-encoded
/// frame per source frame; the app owns no codec (transport-only).
/// </summary>
internal interface IMediaOutboundTrack
{
    /// <summary>Sends one already-encoded frame on this track, stamping the frame's RTP timestamp on the
    /// outbound packets. A no-op until the transport is keyed / the track is negotiated.</summary>
    Task SendFrameAsync(MediaFrame frame, CancellationToken ct = default);
}
