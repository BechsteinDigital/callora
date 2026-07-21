using System.Collections.Concurrent;

namespace Callora.Plugin.Communication.Application.Streaming.Pacing;

/// <summary>
/// Emits queued outbound audio to the call at a steady, clock-driven cadence: a consumer sends TTS
/// audio in bursts, and this releases one frame per <see cref="IPacingClock"/> tick so the call
/// receives a real-time stream. <see cref="Flush"/> drops all queued audio at once — that is
/// barge-in: when the caller starts talking, the agent's pending playback stops immediately. The
/// buffer is capped so a runaway producer cannot grow it without bound.
/// </summary>
public sealed class PacedAudioSender(
    Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> sendFrameAsync,
    IPacingClock clock,
    int maxBufferedFrames = 500)
{
    private readonly ConcurrentQueue<byte[]> _queue = new();

    /// <summary>Queues one outbound frame, dropping the oldest if the safety cap is exceeded.</summary>
    public void Enqueue(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        while (_queue.Count >= maxBufferedFrames && _queue.TryDequeue(out _))
        {
            // Safety cap: bound the buffer so a runaway producer cannot exhaust memory.
        }

        _queue.Enqueue(frame);
    }

    /// <summary>Drops all queued audio — barge-in: the agent's pending playback stops at once.</summary>
    public void Flush()
    {
        while (_queue.TryDequeue(out _))
        {
        }
    }

    /// <summary>Runs the paced emit loop until the clock stops or is cancelled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (await clock.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_queue.TryDequeue(out var frame))
            {
                await sendFrameAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
