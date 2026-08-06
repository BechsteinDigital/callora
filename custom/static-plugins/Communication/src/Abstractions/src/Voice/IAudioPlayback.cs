namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// One announcement being played into a call, for as long as it lasts.
/// </summary>
/// <remarks>
/// <b>Disposing stops it mid-word, and that is the expected course rather than an exception.</b>
/// Whoever knows the PIN types over the greeting, and the greeting has to give way immediately — a
/// consumer that collects DTMF disposes the playback on the first tone. Ending the call, the caller
/// hanging up, and a barge-in all end a playback the same ordinary way.
/// </remarks>
public interface IAudioPlayback : IAsyncDisposable
{
    /// <summary>
    /// Completes when the audio has been played to the end — or when it stopped early, because a
    /// caller who interrupts is not an error and neither is one who hangs up. It does not fault.
    /// </summary>
    Task Completion { get; }
}
