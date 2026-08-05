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
    int maxBufferedFrames = 500,
    int maxBufferedBytes = CommunicationStreamLimits.MaxPacedBufferBytes)
{
    private readonly ConcurrentQueue<byte[]> _queue = new();
    private int _bufferedBytes;

    /// <summary>Bytes currently held in the buffer — the quantity the byte cap bounds.</summary>
    public int BufferedBytes => Volatile.Read(ref _bufferedBytes);

    /// <summary>
    /// Queues one outbound frame, dropping the oldest until both the frame count and
    /// the total byte cap are satisfied. Bounding by count alone was not enough (#108):
    /// a producer sending many large frames stays under the count while the buffer grows.
    /// </summary>
    public void Enqueue(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        _queue.Enqueue(frame);
        Interlocked.Add(ref _bufferedBytes, frame.Length);

        while ((_queue.Count > maxBufferedFrames || Volatile.Read(ref _bufferedBytes) > maxBufferedBytes) &&
               _queue.TryDequeue(out var dropped))
        {
            Interlocked.Add(ref _bufferedBytes, -dropped.Length);
        }
    }

    /// <summary>Drops all queued audio — barge-in: the agent's pending playback stops at once.</summary>
    public void Flush()
    {
        while (_queue.TryDequeue(out var dropped))
        {
            Interlocked.Add(ref _bufferedBytes, -dropped.Length);
        }
    }

    /// <summary>Runs the paced emit loop until the clock stops or is cancelled.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (await clock.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_queue.TryDequeue(out var frame))
            {
                Interlocked.Add(ref _bufferedBytes, -frame.Length);
                await sendFrameAsync(frame, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
