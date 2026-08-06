using System.Collections.Concurrent;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming.Pacing;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// The <see cref="ICallAudioPlayback"/> implementation: resolves the call's live audio stream and
/// releases the announcement into it one frame per tick.
/// </summary>
/// <remarks>
/// It plays into the stream the call already has rather than opening one. Opening would attach a
/// second tap to the same call, and for a caller who is in a conference the announcement would then
/// travel a path of its own — audible perhaps, but not where the rest of that leg's audio goes.
/// </remarks>
internal sealed class CallAudioPlaybackService : ICallAudioPlayback
{
    private readonly ICallAccess _calls;
    private readonly ICallAudioStreamProvider _streams;
    private readonly Func<AudioFormat, IPacingClock> _clockFactory;
    private readonly ConcurrentDictionary<string, CallAudioPlayback> _active = new(StringComparer.Ordinal);

    /// <summary>Creates the service over the call registry, the live streams and the cadence source.</summary>
    public CallAudioPlaybackService(
        ICallAccess calls,
        ICallAudioStreamProvider streams,
        Func<AudioFormat, IPacingClock>? clockFactory = null)
    {
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(streams);

        _calls = calls;
        _streams = streams;
        _clockFactory = clockFactory
            ?? (format => new PeriodicPacingClock(TimeSpan.FromMilliseconds(format.FrameMilliseconds)));
    }

    /// <summary>
    /// Whether an announcement is currently playing into <paramref name="callId"/>. The conference
    /// downlink asks this before each frame so the two do not share the send path.
    /// </summary>
    public bool IsPlaying(string callId) => _active.ContainsKey(callId);

    /// <inheritdoc />
    public async Task<IAudioPlayback> PlayAsync(
        string workspaceKey,
        string callId,
        ReadOnlyMemory<byte> audio,
        AudioFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentNullException.ThrowIfNull(format);

        // The workspace boundary is checked on the call, not on the stream: the stream map is keyed by
        // call id alone, so resolving through it directly would play into another workspace's call.
        if (_calls.Find(workspaceKey, callId) is null)
        {
            throw new InvalidOperationException($"Workspace '{workspaceKey}' has no active call '{callId}'.");
        }

        var stream = await _streams.OpenAsync(callId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Call '{callId}' has no live audio stream to play into.");

        if (stream.Format != format)
        {
            // Playing one G.711 variant down a call carrying the other is not silence — it is loud
            // noise in somebody's ear, so a mismatch fails instead of being sent anyway.
            throw new InvalidOperationException(
                $"Call '{callId}' carries {stream.Format.Codec} at {stream.Format.SampleRateHz} Hz, " +
                $"but the audio is {format.Codec} at {format.SampleRateHz} Hz — the format must match the call's.");
        }

        var playback = new CallAudioPlayback(audio, format, stream.SendAsync, _clockFactory(format));

        // One announcement per call: whatever was playing is replaced, per the contract.
        if (_active.TryRemove(callId, out var previous))
        {
            await previous.DisposeAsync().ConfigureAwait(false);
        }

        _active[callId] = playback;
        playback.Start(() => _active.TryRemove(new KeyValuePair<string, CallAudioPlayback>(callId, playback)));
        return playback;
    }
}
