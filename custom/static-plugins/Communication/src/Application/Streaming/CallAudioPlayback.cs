using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Streaming.Pacing;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// One announcement on its way into a call: the audio cut into frames and released at the call's
/// cadence, until it runs out or somebody interrupts.
/// </summary>
internal sealed class CallAudioPlayback : IAudioPlayback
{
    private readonly ReadOnlyMemory<byte> _audio;
    private readonly AudioFormat _format;
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> _sendAsync;
    private readonly IPacingClock _clock;
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _playing;
    private bool _disposed;

    /// <summary>Prepares the playback; nothing is sent until <see cref="Start"/>.</summary>
    public CallAudioPlayback(
        ReadOnlyMemory<byte> audio,
        AudioFormat format,
        Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> sendAsync,
        IPacingClock clock)
    {
        _audio = audio;
        _format = format;
        _sendAsync = sendAsync;
        _clock = clock;
    }

    /// <inheritdoc />
    public Task Completion => _completion.Task;

    /// <summary>Begins releasing frames; <paramref name="onFinished"/> runs once, however it ends.</summary>
    public void Start(Action onFinished) => _playing = RunAsync(onFinished);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stopping.CancelAsync().ConfigureAwait(false);

        if (_playing is not null)
        {
            await _playing.ConfigureAwait(false);
        }

        _stopping.Dispose();
        (_clock as IDisposable)?.Dispose();
    }

    private async Task RunAsync(Action onFinished)
    {
        try
        {
            var frameSize = _format.BytesPerFrame;
            for (var offset = 0; offset < _audio.Length; offset += frameSize)
            {
                if (!await _clock.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
                {
                    break;
                }

                await _sendAsync(FrameAt(offset, frameSize), _stopping.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Barge-in, or the call ending under the announcement. Both are ordinary endings.
        }
        catch (Exception)
        {
            // A send that fails takes the announcement with it, but not the caller waiting on
            // Completion: they asked when it ended, not whether every frame arrived.
        }
        finally
        {
            onFinished();
            _completion.TrySetResult();
        }
    }

    private ReadOnlyMemory<byte> FrameAt(int offset, int frameSize)
    {
        var remaining = _audio.Length - offset;
        if (remaining >= frameSize)
        {
            return _audio.Slice(offset, frameSize);
        }

        // A trailing partial frame is padded rather than sent short or dropped: short would clip the
        // last word on devices that expect a fixed frame, dropping would swallow it. The cost is at
        // most one frame of silence.
        var padded = new byte[frameSize];
        _audio.Span[offset..].CopyTo(padded);
        return padded;
    }
}
