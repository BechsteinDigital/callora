using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Drives a <see cref="ConferenceDownlinkMixer"/> at the endpoint's frame cadence: one tick, one mixed
/// frame, one send.
/// </summary>
/// <remarks>
/// There is nothing to buffer here, which is why this does not reuse the queue-fed
/// <c>PacedAudioSender</c>: that one releases audio a producer handed it in bursts, whereas the mix is
/// produced on demand. A participant whose frame has not arrived makes the next frame quieter rather
/// than building a backlog, so the leg cannot drift behind the conference.
/// </remarks>
internal sealed class ConferenceDownlinkPump
{
    private readonly ConferenceDownlinkMixer _mixer;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> _sendAsync;
    private readonly IPacingClock _clock;
    private readonly Func<bool>? _isSuppressed;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates the pump over the mix, the endpoint's send path and the cadence source.
    /// </summary>
    public ConferenceDownlinkPump(
        ConferenceDownlinkMixer mixer,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> sendAsync,
        IPacingClock clock,
        ILogger? logger = null,
        Func<bool>? isSuppressed = null)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentNullException.ThrowIfNull(sendAsync);
        ArgumentNullException.ThrowIfNull(clock);

        _mixer = mixer;
        _sendAsync = sendAsync;
        _clock = clock;
        _isSuppressed = isSuppressed;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Runs until the clock stops or the token is cancelled — a hung-up call ends this loop the same
    /// ordinary way a stopped clock does, so neither is reported as a fault.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (await _clock.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                // An announcement owns this leg's send path while it plays: two senders on one stream
                // interleave into something that is neither the room nor the announcement. The mix is
                // still consumed, so the leg does not resume with a frame from before the interruption.
                var frame = _mixer.NextFrame();
                if (_isSuppressed?.Invoke() == true)
                {
                    continue;
                }

                await _sendAsync(frame, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // One failed send is 20 ms of audio. Ending the leg over it would turn a transport
                // hiccup into a dropped call, so the frame is lost and the next tick tries again.
                _logger.LogDebug(ex, "ConferenceDownlinkPump: sending a mixed frame failed — frame dropped.");
            }
        }
    }
}
