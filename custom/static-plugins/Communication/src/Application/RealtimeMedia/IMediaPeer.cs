using Callora.Plugin.Communication.Abstractions.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.RealtimeMedia;

/// <summary>
/// A neutral server-side real-time media peer — the modality-neutral counterpart to a WebRTC peer
/// connection, and the only surface the media layer (calls, SFU) builds on. It negotiates via SDP/ICE,
/// carries send-only outbound tracks and inbound remote tracks of encoded frames, and bridges downstream
/// key-frame requests (PLI). A provider adapter wraps one SDK peer as this port; no SDK type leaks through.
/// </summary>
/// <remarks>
/// The peer owns its lifetime: <see cref="IAsyncDisposable.DisposeAsync"/> tears down ICE/DTLS/RTP and
/// detaches the adapter's SDK event handlers. <see cref="KeyFrameRequested"/> carries no MID (an SDK
/// limitation — the downstream PLI is not attributed to a specific upstream track), so a forwarding layer
/// requests a key frame from every upstream.
/// </remarks>
internal interface IMediaPeer : IAsyncDisposable
{
    /// <summary>The current lifecycle state of the peer.</summary>
    MediaConnectionState ConnectionState { get; }

    /// <summary>Raised when <see cref="ConnectionState"/> transitions.</summary>
    event EventHandler<MediaConnectionState>? ConnectionStateChanged;

    /// <summary>Raised with each locally gathered ICE candidate, for the consumer to relay to the browser.</summary>
    event EventHandler<IceCandidate>? LocalIceCandidateDiscovered;

    /// <summary>Raised once per remote track when it is first materialised (the W3C track event); subscribe
    /// to the track's <see cref="IRemoteMediaTrack.FrameReceived"/> synchronously in the handler.</summary>
    event EventHandler<IRemoteMediaTrack>? RemoteTrackReceived;

    /// <summary>Raised when the downstream browser requests a key frame (PLI). Carries no MID — the SDK does
    /// not attribute the request to a specific upstream track (a deliberate limitation).</summary>
    event EventHandler? KeyFrameRequested;

    /// <summary>Produces a local offer (BUNDLE, DTLS-SRTP, ICE) for the consumer to signal out.</summary>
    SessionDescription CreateOffer();

    /// <summary>Applies a remote description. Returns the local answer when this peer is the answerer; when
    /// it is the offerer applying the peer's answer, returns the local offer unchanged (analog to the SDK).</summary>
    Task<SessionDescription?> ApplyRemoteDescriptionAsync(SessionDescription remote, CancellationToken ct = default);

    /// <summary>Applies one remote ICE candidate.</summary>
    Task AddIceCandidateAsync(IceCandidate candidate, CancellationToken ct = default);

    /// <summary>Gathers local ICE candidates (raised via <see cref="LocalIceCandidateDiscovered"/>).</summary>
    Task GatherCandidatesAsync(CancellationToken ct = default);

    /// <summary>Starts the peer's transport (ICE/DTLS).</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Adds a send-only outbound track of the given kind, carrying <paramref name="streamId"/> as its
    /// MediaStream id (the source participant for SFU forwarding). Must be called before the first offer.</summary>
    IMediaOutboundTrack AddOutboundTrack(MediaTrackKind kind, string streamId);

    /// <summary>Requests a fresh video key frame from the peer (PLI). Tolerant: a no-op returning
    /// <see langword="false"/> when no video is negotiated or the throttle holds; <see langword="true"/> when
    /// a PLI was sent.</summary>
    ValueTask<bool> RequestKeyFrameAsync(CancellationToken ct = default);
}
