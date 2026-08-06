namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Turns a conference's N forwarded streams into the single stream an endpoint that cannot mix — a
/// telephone — is able to receive: decode each participant to PCM16, sum them, encode to the
/// endpoint's codec. One instance serves one such endpoint.
/// </summary>
/// <remarks>
/// <para><b>One frame in, one frame out.</b> Each pushed frame contributes to exactly one outbound
/// frame and is then consumed. Replaying the last frame when a source falls silent would stutter that
/// speaker; contributing nothing is the honest reading of "no audio arrived".</para>
/// <para><b>No jitter buffer.</b> Both sides run on a 20 ms cadence, so a source that pushes twice
/// between two outbound frames loses the earlier one. Absorbing that needs a real jitter buffer with
/// its own latency budget, which is a decision for measurements rather than for this class.</para>
/// <para><b>Not thread-safe.</b> Pushes arrive on media receive callbacks and
/// <see cref="NextFrame"/> runs on the send cadence; the caller serializes them.</para>
/// </remarks>
internal sealed class ConferenceDownlinkMixer : IDisposable
{
    private readonly Dictionary<string, ConferenceDownlinkSource> _sources = new(StringComparer.Ordinal);
    private readonly IAudioTranscoderFactory _transcoders;
    private readonly ConferenceAudioCodec _sourceCodec;
    private readonly IAudioTranscoder _outboundEncoder;
    private readonly int _pcmSampleRate;
    private readonly int _samplesPerFrame;
    private bool _disposed;

    /// <summary>
    /// Creates the mixer for one endpoint.
    /// </summary>
    /// <param name="transcoders">Creates the per-source decoders and the outbound encoder.</param>
    /// <param name="sourceCodec">The codec the conference participants are encoded in.</param>
    /// <param name="outboundCodec">The codec the endpoint receives.</param>
    /// <param name="pcmSampleRate">The PCM rate mixing happens at — the endpoint's rate, so its own leg needs no resampling.</param>
    /// <param name="samplesPerFrame">Samples per outbound frame (160 for 20 ms at 8 kHz).</param>
    public ConferenceDownlinkMixer(
        IAudioTranscoderFactory transcoders,
        ConferenceAudioCodec sourceCodec,
        ConferenceAudioCodec outboundCodec,
        int pcmSampleRate,
        int samplesPerFrame)
    {
        ArgumentNullException.ThrowIfNull(transcoders);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(samplesPerFrame);

        _transcoders = transcoders;
        _sourceCodec = sourceCodec;
        _pcmSampleRate = pcmSampleRate;
        _samplesPerFrame = samplesPerFrame;
        _outboundEncoder = transcoders.Create(outboundCodec, pcmSampleRate);
    }

    /// <summary>Starts mixing <paramref name="participantId"/>, giving it its own decoder state.</summary>
    public void AddSource(string participantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);

        if (_sources.ContainsKey(participantId))
        {
            return;
        }

        _sources[participantId] = new ConferenceDownlinkSource(
            _transcoders.Create(_sourceCodec, _pcmSampleRate));
    }

    /// <summary>Stops mixing <paramref name="participantId"/> and releases its decoder.</summary>
    public void RemoveSource(string participantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);

        if (_sources.Remove(participantId, out var source))
        {
            source.Dispose();
        }
    }

    /// <summary>
    /// Offers one encoded frame from <paramref name="participantId"/> for the next outbound frame. A
    /// frame from a participant that is not (or no longer) mixed is dropped: it arrived in flight
    /// behind their departure, and mixing it would put a departed participant back into the room.
    /// </summary>
    public void Push(string participantId, ReadOnlySpan<byte> encodedPayload)
    {
        if (!_sources.TryGetValue(participantId, out var source))
        {
            return;
        }

        source.Pending = source.Decoder.DecodeToPcm16(encodedPayload);
    }

    /// <summary>
    /// Produces the endpoint's next frame from everything pushed since the previous one, consuming it.
    /// </summary>
    public byte[] NextFrame()
    {
        var contributions = new List<Pcm16Contribution>(_sources.Count);

        foreach (var source in _sources.Values)
        {
            if (source.Pending is { Length: > 0 } pending)
            {
                contributions.Add(new Pcm16Contribution(pending, 1f));
            }

            source.Pending = null;
        }

        var mixed = new byte[_samplesPerFrame * 2];
        Pcm16Mixer.Mix([.. contributions], mixed);
        return _outboundEncoder.EncodeFromPcm16(mixed);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (var source in _sources.Values)
        {
            source.Dispose();
        }

        _sources.Clear();
        _outboundEncoder.Dispose();
    }
}
