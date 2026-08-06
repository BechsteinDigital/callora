namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// One participant's audio going into a mix, as PCM16 little-endian samples with the gain to apply.
/// </summary>
/// <param name="Pcm16">
/// The contributor's decoded samples for this frame, PCM16 little-endian. May be shorter than the
/// destination — a contributor whose frame has not arrived yet contributes only what it has.
/// </param>
/// <param name="Gain">
/// Linear gain, <c>0</c> silencing the contribution entirely. Server-side mute lives here: a host's
/// force-mute must not depend on the muted device honouring it.
/// </param>
internal readonly record struct Pcm16Contribution(ReadOnlyMemory<byte> Pcm16, float Gain);
