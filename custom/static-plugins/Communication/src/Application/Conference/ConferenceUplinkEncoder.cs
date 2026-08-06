namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Carries a single-stream endpoint's audio into a conference: decode its codec to PCM16, re-encode in
/// the conference's codec, and stamp the RTP timestamp the forwarding path passes through unchanged.
/// The endpoint then appears to the SFU and to every browser as an ordinary participant.
/// </summary>
/// <remarks>
/// <para><b>The RTP clock is not the PCM rate.</b> Opus stamps at 48 kHz whatever rate it codes at
/// (RFC 7587 §4.1), so a 20 ms frame advances the timestamp by 960 rather than by the 160 samples the
/// telephony-rate PCM holds. Stamping the sample count would play the leg six times too fast and drift
/// it against every other participant's A/V sync.</para>
/// <para><b>Not thread-safe.</b> Frames arrive on the media receive callback; the caller serializes.</para>
/// </remarks>
internal sealed class ConferenceUplinkEncoder : IDisposable
{
    private const int OpusRtpClockRate = 48_000;

    private readonly IAudioTranscoder _decoder;
    private readonly IAudioTranscoder _encoder;
    private readonly uint _ticksPerFrame;
    private uint _rtpTimestamp;
    private bool _disposed;

    /// <summary>
    /// Creates the uplink for one endpoint.
    /// </summary>
    /// <param name="transcoders">Creates the inbound decoder and the outbound encoder.</param>
    /// <param name="endpointCodec">The codec the endpoint sends in.</param>
    /// <param name="conferenceCodec">The codec the conference participants expect.</param>
    /// <param name="pcmSampleRate">The PCM rate the transcoding runs at — the endpoint's, so its leg needs no resampling.</param>
    /// <param name="samplesPerFrame">Samples per inbound frame (160 for 20 ms at 8 kHz).</param>
    public ConferenceUplinkEncoder(
        IAudioTranscoderFactory transcoders,
        ConferenceAudioCodec endpointCodec,
        ConferenceAudioCodec conferenceCodec,
        int pcmSampleRate,
        int samplesPerFrame)
    {
        ArgumentNullException.ThrowIfNull(transcoders);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pcmSampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samplesPerFrame);

        _decoder = transcoders.Create(endpointCodec, pcmSampleRate);
        _encoder = transcoders.Create(conferenceCodec, pcmSampleRate);
        _ticksPerFrame = TicksPerFrame(conferenceCodec, pcmSampleRate, samplesPerFrame);
    }

    /// <summary>
    /// Whether this leg's audio is withheld from the conference. Enforced here rather than asked of the
    /// endpoint: a host's force-mute must not depend on a device that has no way to honour it.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Transcodes one inbound frame for the conference, or returns <see langword="null"/> while muted.
    /// The timestamp advances either way, so a mute reads as a gap of silence rather than as audio
    /// that belongs in the past once it resumes.
    /// </summary>
    public ConferenceUplinkFrame? Encode(ReadOnlySpan<byte> endpointPayload)
    {
        var timestamp = _rtpTimestamp;
        _rtpTimestamp += _ticksPerFrame;

        if (IsMuted)
        {
            return null;
        }

        var pcm = _decoder.DecodeToPcm16(endpointPayload);
        return new ConferenceUplinkFrame(_encoder.EncodeFromPcm16(pcm), timestamp);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _decoder.Dispose();
        _encoder.Dispose();
    }

    private static uint TicksPerFrame(ConferenceAudioCodec codec, int pcmSampleRate, int samplesPerFrame)
    {
        // Opus is the one codec whose RTP clock is fixed independently of the coded rate; for the rest
        // the clock is the sample rate, so the frame's sample count is the tick count.
        var clockRate = codec == ConferenceAudioCodec.Opus ? OpusRtpClockRate : pcmSampleRate;
        return (uint)((long)samplesPerFrame * clockRate / pcmSampleRate);
    }
}
