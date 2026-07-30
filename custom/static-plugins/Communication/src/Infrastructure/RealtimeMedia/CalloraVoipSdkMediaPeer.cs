using Callora.Plugin.Communication.Application.RealtimeMedia;
using Callora.Plugin.Communication.Abstractions.RealtimeMedia;
using CalloraVoipSdk.WebRtc;
using SdkIceCandidate = Callora.Plugin.Communication.Abstractions.RealtimeMedia.IceCandidate;

namespace Callora.Plugin.Communication.Infrastructure.RealtimeMedia;

/// <summary>
/// Adapts one CalloraVoipSdk <see cref="IPeerConnection"/> to the neutral <see cref="IMediaPeer"/> port:
/// negotiation (offer/answer/candidate), send-only outbound tracks, inbound remote tracks of encoded frames,
/// and the downstream key-frame (PLI) bridge. It maps the SDK's <see cref="PeerConnectionState"/> onto the
/// neutral <see cref="MediaConnectionState"/> and translates the SDK's string SDP/candidate wire values to
/// and from the neutral <see cref="SessionDescription"/>/<see cref="SdkIceCandidate"/>. It is the boundary at
/// which no <c>CalloraVoipSdk</c> type escapes upward.
/// </summary>
/// <remarks>
/// The adapter subscribes to the peer's lifecycle/candidate/track/PLI events on construction and detaches
/// them on <see cref="DisposeAsync"/> before disposing the peer, so it never outlives the connection.
/// <see cref="KeyFrameRequested"/> carries no MID: the SDK's downstream PLI is not attributed to an upstream
/// track, matching the port's deliberate limitation.
/// </remarks>
internal sealed class CalloraVoipSdkMediaPeer : IMediaPeer
{
    private readonly IPeerConnection _peer;
    // Wraps the inbound remote tracks so their per-track FrameReceived subscriptions are detached
    // deterministically on teardown (not left to the SDK dropping the track). Mutated under _sync because
    // OnTrackReceived fires on an SDK thread while Detach runs on the dispose path.
    private readonly object _sync = new();
    private readonly List<CalloraVoipSdkRemoteTrack> _remoteTracks = [];
    private bool _detached;

    /// <summary>Wraps <paramref name="peer"/> as a neutral media peer and hooks its SDK events.</summary>
    public CalloraVoipSdkMediaPeer(IPeerConnection peer)
    {
        ArgumentNullException.ThrowIfNull(peer);
        _peer = peer;

        _peer.ConnectionStateChanged += OnConnectionStateChanged;
        _peer.LocalIceCandidateDiscovered += OnLocalIceCandidate;
        _peer.TrackReceived += OnTrackReceived;
        _peer.VideoKeyFrameRequested += OnVideoKeyFrameRequested;
    }

    /// <inheritdoc />
    public MediaConnectionState ConnectionState => MapState(_peer.State);

    /// <inheritdoc />
    public event EventHandler<MediaConnectionState>? ConnectionStateChanged;

    /// <inheritdoc />
    public event EventHandler<SdkIceCandidate>? LocalIceCandidateDiscovered;

    /// <inheritdoc />
    public event EventHandler<IRemoteMediaTrack>? RemoteTrackReceived;

    /// <inheritdoc />
    public event EventHandler? KeyFrameRequested;

    /// <inheritdoc />
    public SessionDescription CreateOffer() => new("offer", _peer.CreateOffer());

    /// <inheritdoc />
    public async Task<SessionDescription?> ApplyRemoteDescriptionAsync(
        SessionDescription remote,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(remote);

        // The SDK returns the local answer when this peer is the answerer, or the local offer unchanged when
        // it is the offerer applying the peer's answer. Surface it as an answer; the caller (which drives the
        // role) knows whether it needs it.
        var sdp = await _peer.SetRemoteDescriptionAsync(remote.Sdp, ct).ConfigureAwait(false);
        return new SessionDescription("answer", sdp);
    }

    /// <inheritdoc />
    public Task AddIceCandidateAsync(SdkIceCandidate candidate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return _peer.AddIceCandidateAsync(candidate.Candidate, ct);
    }

    /// <inheritdoc />
    public Task GatherCandidatesAsync(CancellationToken ct = default) => _peer.GatherCandidatesAsync(ct);

    /// <inheritdoc />
    public Task StartAsync(CancellationToken ct = default) => _peer.StartAsync(ct);

    /// <inheritdoc />
    public IMediaOutboundTrack AddOutboundTrack(MediaTrackKind kind, string streamId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(streamId);

        return kind switch
        {
            MediaTrackKind.Video => new CalloraVoipSdkVideoOutboundTrack(
                _peer.AddVideoTrack(new VideoTrackOptions
                {
                    Direction = TrackDirection.SendOnly,
                    StreamId = streamId,
                })),
            MediaTrackKind.Audio => new CalloraVoipSdkAudioOutboundTrack(
                _peer.AddAudioTrack(new AudioTrackOptions
                {
                    Direction = TrackDirection.SendOnly,
                    StreamId = streamId,
                })),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown media track kind."),
        };
    }

    /// <inheritdoc />
    public ValueTask<bool> RequestKeyFrameAsync(CancellationToken ct = default) =>
        _peer.RequestVideoKeyFrameAsync(ct);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_detached)
        {
            return;
        }

        Detach();
        await _peer.DisposeAsync().ConfigureAwait(false);
    }

    private void OnConnectionStateChanged(object? sender, PeerConnectionState state) =>
        ConnectionStateChanged?.Invoke(this, MapState(state));

    private void OnLocalIceCandidate(object? sender, string candidate) =>
        LocalIceCandidateDiscovered?.Invoke(this, new SdkIceCandidate(candidate));

    private void OnTrackReceived(object? sender, RemoteTrack track)
    {
        var wrapper = new CalloraVoipSdkRemoteTrack(track);
        lock (_sync)
        {
            if (_detached)
            {
                // Disposal raced this inbound track: tear the fresh subscription down at once instead of
                // tracking it, so a track arriving during teardown never leaks its FrameReceived hook.
                wrapper.Detach();
                return;
            }

            _remoteTracks.Add(wrapper);
        }

        RemoteTrackReceived?.Invoke(this, wrapper);
    }

    private void OnVideoKeyFrameRequested(object? sender, EventArgs e) =>
        KeyFrameRequested?.Invoke(this, EventArgs.Empty);

    private void Detach()
    {
        List<CalloraVoipSdkRemoteTrack> remoteTracks;
        lock (_sync)
        {
            if (_detached)
            {
                return;
            }

            _detached = true;
            _peer.ConnectionStateChanged -= OnConnectionStateChanged;
            _peer.LocalIceCandidateDiscovered -= OnLocalIceCandidate;
            _peer.TrackReceived -= OnTrackReceived;
            _peer.VideoKeyFrameRequested -= OnVideoKeyFrameRequested;
            remoteTracks = [.. _remoteTracks];
            _remoteTracks.Clear();
        }

        // Detach each inbound track's FrameReceived hook outside the lock (the handler side never re-enters).
        foreach (var track in remoteTracks)
        {
            track.Detach();
        }
    }

    private static MediaConnectionState MapState(PeerConnectionState state) => state switch
    {
        PeerConnectionState.New => MediaConnectionState.New,
        PeerConnectionState.Connecting => MediaConnectionState.Connecting,
        PeerConnectionState.Connected => MediaConnectionState.Connected,
        PeerConnectionState.Disconnected => MediaConnectionState.Disconnected,
        PeerConnectionState.Failed => MediaConnectionState.Failed,
        PeerConnectionState.Closed => MediaConnectionState.Closed,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown WebRTC peer state."),
    };
}
