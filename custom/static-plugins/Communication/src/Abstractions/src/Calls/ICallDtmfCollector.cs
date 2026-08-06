namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Collects a multi-digit entry from a call's DTMF tones — a conference PIN, an IVR selection, a
/// customer number.
/// </summary>
/// <remarks>
/// <para>This exists for the same reason <see cref="ICallAudioPlayback"/> does: every consumer would
/// otherwise rebuild the same three awkward parts. Tones arrive duplicated by design, from two threads
/// with no ordering promise, and the end of an entry is genuinely ambiguous — a full-length entry
/// completes itself, a short one needs a key, and silence needs a deadline.</para>
/// <para><b>One call collects one entry.</b> Repetition, attempt limits and what the digits mean stay
/// with the consumer: those are policy, and policy differs between a conference PIN and an IVR menu.</para>
/// </remarks>
public interface ICallDtmfCollector
{
    /// <summary>
    /// Collects one entry from <paramref name="callId"/>, replacing any collection already running on
    /// that call.
    /// </summary>
    /// <param name="workspaceKey">The workspace the caller acts for; the call must belong to it.</param>
    /// <param name="callId">The live call to collect from.</param>
    /// <param name="options">Length, pause, and the keys that submit and clear.</param>
    /// <param name="cancellationToken">Ends the collection with <see cref="DtmfEntryOutcome.Superseded"/>.</param>
    /// <exception cref="InvalidOperationException">The workspace has no such active call.</exception>
    Task<DtmfEntry> CollectAsync(
        string workspaceKey,
        string callId,
        DtmfCollectOptions options,
        CancellationToken cancellationToken = default);
}
