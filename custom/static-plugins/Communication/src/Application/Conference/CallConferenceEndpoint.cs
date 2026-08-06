using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// A call taking part in a conference as an ordinary participant. It satisfies
/// <see cref="IConferenceEndpoint"/> with a mix and a transcoder where a browser participant has a
/// WebRTC transport, so the forwarding path treats it identically to every other member — which is
/// what makes a telephone a participant rather than a special case.
/// </summary>
/// <remarks>
/// <para>Video is accepted and dropped. The router adds a video track per source unconditionally, and
/// refusing one would make this endpoint a special case in exactly the code that must not have any.
/// Sending video to a phone is a later stage: switching the active speaker's stream needs no
/// transcoding at all, since the SDK and browsers share codecs.</para>
/// <para><b>Not thread-safe.</b> Frames arrive from the media receive callbacks of other participants
/// and from the call; the attachment serializes access.</para>
/// </remarks>
internal sealed class CallConferenceEndpoint : IConferenceEndpoint, IDisposable
{
    private readonly ConferenceDownlinkMixer _mixer;
    private readonly ConferenceUplinkEncoder _uplink;
    private readonly ConferenceUplinkTrack _inbound;
    private bool _started;
    private bool _disposed;

    /// <summary>
    /// Creates the endpoint over the mix it feeds and the transcoding it needs.
    /// </summary>
    /// <param name="mixer">Where the other participants' frames go; owned by the caller.</param>
    /// <param name="transcoders">Creates the uplink's decoder and encoder.</param>
    /// <param name="callCodec">The codec the call carries.</param>
    /// <param name="conferenceCodec">The codec the conference participants expect.</param>
    /// <param name="pcmSampleRate">The PCM rate transcoding runs at.</param>
    /// <param name="samplesPerFrame">Samples per frame on the call's side.</param>
    /// <param name="participantId">This participant's id, carried as the inbound track's stream id.</param>
    public CallConferenceEndpoint(
        ConferenceDownlinkMixer mixer,
        IAudioTranscoderFactory transcoders,
        ConferenceAudioCodec callCodec,
        ConferenceAudioCodec conferenceCodec,
        int pcmSampleRate,
        int samplesPerFrame,
        string participantId = "call")
    {
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentNullException.ThrowIfNull(transcoders);

        _mixer = mixer;
        _uplink = new ConferenceUplinkEncoder(transcoders, callCodec, conferenceCodec, pcmSampleRate, samplesPerFrame);
        _inbound = new ConferenceUplinkTrack(participantId);
    }

    /// <summary>Whether this leg's audio is withheld from the conference; enforced server-side.</summary>
    public bool IsMuted
    {
        get => _uplink.IsMuted;
        set => _uplink.IsMuted = value;
    }

    /// <inheritdoc />
    /// <remarks>A connected call has no handshake left to wait for, so the router's readiness gate opens.</remarks>
    public MediaConnectionState ConnectionState => MediaConnectionState.Connected;

    /// <inheritdoc />
    public event EventHandler<IRemoteMediaTrack>? RemoteTrackReceived;

    /// <inheritdoc />
    /// <remarks>Never raised: a telephone has no decoder whose picture could need repairing.</remarks>
    public event EventHandler? KeyFrameRequested
    {
        add { }
        remove { }
    }

    /// <summary>
    /// Surfaces this endpoint's inbound track, after the router has subscribed. Separate from the
    /// constructor because the router subscribes while wiring the topology, and a track raised before
    /// that would be missed.
    /// </summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        RemoteTrackReceived?.Invoke(this, _inbound);
    }

    /// <summary>
    /// Offers one frame from the call to the conference. Dropped while muted, and before
    /// <see cref="Start"/> because nothing is listening yet.
    /// </summary>
    public void PushFromCall(ReadOnlySpan<byte> callPayload)
    {
        if (!_started || _uplink.Encode(callPayload) is not { } frame)
        {
            return;
        }

        _inbound.Raise(new MediaFrame(frame.Payload, frame.RtpTimestamp, IsKeyFrame: false, _inbound.StreamId));
    }

    /// <inheritdoc />
    public IMediaOutboundTrack AddOutboundTrack(MediaTrackKind kind, string streamId)
    {
        if (kind != MediaTrackKind.Audio)
        {
            return new DiscardingMediaOutboundTrack();
        }

        _mixer.AddSource(streamId);
        return new ConferenceMixInputTrack(_mixer, streamId);
    }

    /// <inheritdoc />
    /// <remarks>Refused rather than ignored: this endpoint receives no video, so claiming a key frame
    /// was sent would tell the router something untrue.</remarks>
    public ValueTask<bool> RequestKeyFrameAsync(CancellationToken ct = default) => ValueTask.FromResult(false);

    /// <inheritdoc />
    /// <remarks>A topology change re-offers to a browser. There is no SDP on this leg, so nothing is
    /// owed — reporting an error here would surface on every join and leave in the room.</remarks>
    public Task RenegotiateAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uplink.Dispose();
    }
}
