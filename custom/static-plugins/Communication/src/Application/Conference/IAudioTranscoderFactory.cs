namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Creates the per-stream <see cref="IAudioTranscoder"/> instances the conference bridge needs — one
/// per participant per direction. The seam that keeps the mixing layer free of a media SDK (ADR-016)
/// and testable without one.
/// </summary>
internal interface IAudioTranscoderFactory
{
    /// <summary>
    /// Creates a transcoder for <paramref name="codec"/> operating on PCM16 at
    /// <paramref name="pcmSampleRate"/> Hz. Fixed-rate codecs reject a rate that is not theirs rather
    /// than producing mis-rated audio.
    /// </summary>
    IAudioTranscoder Create(ConferenceAudioCodec codec, int pcmSampleRate);
}
