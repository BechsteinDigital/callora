namespace Callora.Plugin.Communication.Abstractions.Conference;

/// <summary>
/// What a conference requires of anything that wants to take part in it. Set by the vertical when it
/// opens a room, because only the vertical knows what kind of room it is — a public standup and a
/// medical consultation carry different obligations.
/// </summary>
/// <param name="RequiresEndToEndEncryption">
/// Whether this conference may only carry media that stays encrypted end to end.
/// <para>
/// <b>This does not encrypt anything.</b> It states an obligation the room is under; the media path
/// has to satisfy it separately. Today it has exactly one effect, and it is a real one: a participant
/// that makes end-to-end encryption impossible cannot join. A telephone is such a participant — the
/// server has to decrypt in order to transcode and mix for it, so a room either keeps that guarantee
/// or has phone participants, never both. Setting this is how a room chooses the guarantee.
/// </para>
/// </param>
public sealed record ConferencePolicy(bool RequiresEndToEndEncryption = false)
{
    /// <summary>A conference under no additional obligation — the default when none is stated.</summary>
    public static ConferencePolicy Unrestricted { get; } = new();
}
