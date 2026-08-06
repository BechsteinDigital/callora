using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// One participant as the conference's forwarding path sees it: somewhere frames arrive from, and
/// somewhere per-source tracks can be added to send frames to.
/// </summary>
/// <remarks>
/// <para>This is deliberately everything the router needs and nothing more. A browser participant
/// satisfies it through a WebRTC peer, which is the only shape that existed while browsers were the
/// only members. An endpoint that cannot mix for itself — a telephone receives a single stream —
/// satisfies it by decoding and mixing behind the same members, and the forwarding path does not
/// change to accommodate it.</para>
/// <para>The negotiation half of a participant stays out of this contract: an endpoint that carries no
/// SDP still has to answer <see cref="RenegotiateAsync"/>, and for it the answer is that a topology
/// change costs it nothing.</para>
/// </remarks>
internal interface IConferenceEndpoint
{
    /// <summary>Whether this endpoint can currently receive media. Frames are not sent to it before.</summary>
    MediaConnectionState ConnectionState { get; }

    /// <summary>Raised once per inbound track; the router subscribes to its frames for fan-out.</summary>
    event EventHandler<IRemoteMediaTrack>? RemoteTrackReceived;

    /// <summary>Raised when this endpoint needs a fresh key frame from the sources it renders.</summary>
    event EventHandler? KeyFrameRequested;

    /// <summary>
    /// Adds a send-only track carrying <paramref name="streamId"/>'s media to this endpoint.
    /// </summary>
    IMediaOutboundTrack AddOutboundTrack(MediaTrackKind kind, string streamId);

    /// <summary>Asks this endpoint's source for a key frame. Tolerant: <see langword="false"/> when it has no video.</summary>
    ValueTask<bool> RequestKeyFrameAsync(CancellationToken ct = default);

    /// <summary>Applies a topology change — for a peer a new offer, for a local endpoint a no-op.</summary>
    Task RenegotiateAsync(CancellationToken ct = default);
}
