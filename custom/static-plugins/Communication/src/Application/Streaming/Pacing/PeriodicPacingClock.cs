namespace Callora.Plugin.Communication.Application.Streaming.Pacing;

/// <summary>
/// Monotone <see cref="IPacingClock"/> backed by <see cref="PeriodicTimer"/>: a steady cadence with
/// no drift accumulation and no <c>Task.Delay</c> (§5.3). <see cref="PeriodicTimer"/> schedules each
/// tick off the runtime's monotonic clock, so pacing does not drift even under scheduling jitter.
/// </summary>
public sealed class PeriodicPacingClock : IPacingClock, IDisposable
{
    private readonly PeriodicTimer _timer;

    /// <summary>Creates a clock ticking every <paramref name="interval"/> (the audio frame length).</summary>
    public PeriodicPacingClock(TimeSpan interval)
    {
        _timer = new PeriodicTimer(interval);
    }

    /// <inheritdoc />
    public async ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _timer.Dispose();
}
