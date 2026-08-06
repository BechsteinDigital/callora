namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// One DTMF tone received from the remote party, carried by <see cref="ICall.DtmfReceived"/>.
/// </summary>
/// <remarks>
/// <para><b>Duplicates are normal, not exceptional.</b> The same physical keypress is routinely
/// reported more than once — in-band echo of a tone the remote party also signalled out-of-band, and
/// RFC 4733 retransmissions of the same event. This contract does <b>not</b> de-duplicate: every
/// reported tone is raised. A consumer that counts keypresses (PIN entry, IVR menus) must decide
/// for itself whether a repeat is a second press or an echo — typically by ignoring tones that
/// arrive while it is busy resolving a previous entry, or by comparing <see cref="DurationMs"/>
/// against the tone's expected length.</para>
/// </remarks>
/// <param name="tone">The received tone: <c>0</c>-<c>9</c>, <c>*</c>, <c>#</c> or <c>A</c>-<c>D</c>.</param>
/// <param name="durationMs">The tone's reported duration in milliseconds.</param>
public sealed class DtmfReceivedEventArgs(char tone, int durationMs) : EventArgs
{
    /// <summary>The received tone: <c>0</c>-<c>9</c>, <c>*</c>, <c>#</c> or <c>A</c>-<c>D</c>.</summary>
    public char Tone { get; } = tone;

    /// <summary>
    /// The tone's reported duration in milliseconds — one signal a consumer can use to tell a long
    /// keypress from a duplicate report of a short one.
    /// </summary>
    public int DurationMs { get; } = durationMs;
}
