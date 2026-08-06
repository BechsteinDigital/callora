namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Plays prepared audio into a live call — the mechanics under any announcement, whoever produces it.
/// </summary>
/// <remarks>
/// <para>This is deliberately not text-to-speech. Turning words into audio is a service of its own and
/// belongs in its own plugin; what lives here is pushing bytes into a call at the right cadence,
/// because that is what every announcing consumer would otherwise rebuild — a dial-in, an IVR, a voice
/// agent, an answering machine.</para>
/// <para>The audio is already encoded in the call's own format, so nothing is transcoded on the way
/// out. For the telephony default that means the file is literally the byte stream the call carries.
/// A producer working at another rate converts once when it synthesises, not on every playback.</para>
/// </remarks>
public interface ICallAudioPlayback
{
    /// <summary>
    /// Starts playing <paramref name="audio"/> into the call, replacing whatever this call was
    /// playing.
    /// </summary>
    /// <remarks>
    /// Replacing rather than queueing is the deliberate choice: a queue makes the device sound like it
    /// is repeating itself, and nobody wants to hear "please enter your PIN" after they already have.
    /// A consumer that does want one announcement after another awaits
    /// <see cref="IAudioPlayback.Completion"/> and then plays the next.
    /// </remarks>
    /// <param name="workspaceKey">The workspace the caller acts for; the call must belong to it.</param>
    /// <param name="callId">The live call to play into.</param>
    /// <param name="audio">The audio, already encoded in <paramref name="format"/>.</param>
    /// <param name="format">The format of <paramref name="audio"/>; must be the call's own.</param>
    /// <param name="cancellationToken">Cancels starting the playback.</param>
    /// <exception cref="InvalidOperationException">
    /// The workspace has no such active call, the call has no live audio, or the format is not the
    /// one the call carries.
    /// </exception>
    Task<IAudioPlayback> PlayAsync(
        string workspaceKey,
        string callId,
        ReadOnlyMemory<byte> audio,
        AudioFormat format,
        CancellationToken cancellationToken = default);
}
