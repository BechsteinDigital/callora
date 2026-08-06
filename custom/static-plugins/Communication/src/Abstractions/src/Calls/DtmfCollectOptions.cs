namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// How one DTMF entry is collected.
/// </summary>
/// <param name="Length">
/// How many digits a complete entry has. Reaching it completes the collection on its own, because
/// most callers never press a submit key.
/// </param>
/// <param name="InterDigitTimeout">
/// How long the caller may pause before the collection gives up. Without it a silent line stays open
/// forever.
/// </param>
/// <param name="SubmitKey">Ends a shorter entry, or <see langword="null"/> to disable.</param>
/// <param name="ClearKey">
/// Discards what was typed and ends the collection as <see cref="DtmfEntryOutcome.Cleared"/>, or
/// <see langword="null"/> to disable. It is the first thing anyone reaches for after a mistype.
/// </param>
/// <param name="DuplicateWindow">
/// How close two identical tones must be to count as one keypress. The transport reports the same
/// press more than once (in-band echo, RFC 4733 retransmissions), while two deliberate presses of the
/// same key are far slower than any echo — so a short window separates them. Raise it for a noisy
/// trunk, lower it for callers who type fast.
/// </param>
public sealed record DtmfCollectOptions(
    int Length,
    TimeSpan InterDigitTimeout,
    char? SubmitKey = '#',
    char? ClearKey = '*',
    TimeSpan? DuplicateWindow = null)
{
    /// <summary>The default duplicate window: long enough for an echo, far shorter than a second press.</summary>
    public static TimeSpan DefaultDuplicateWindow { get; } = TimeSpan.FromMilliseconds(120);

    /// <summary>The window in force, falling back to <see cref="DefaultDuplicateWindow"/>.</summary>
    public TimeSpan EffectiveDuplicateWindow => DuplicateWindow ?? DefaultDuplicateWindow;
}
