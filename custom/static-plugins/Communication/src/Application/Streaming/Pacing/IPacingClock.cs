namespace Callora.Plugin.Communication.Application.Streaming.Pacing;

/// <summary>
/// A monotone pacing source that releases one tick per audio-frame interval. Abstracted so the
/// <see cref="PacedAudioSender"/> can be driven by a real timer in production and deterministically
/// in tests.
/// </summary>
public interface IPacingClock
{
    /// <summary>Awaits the next tick; returns <see langword="false"/> once the clock is stopped or cancelled.</summary>
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default);
}
