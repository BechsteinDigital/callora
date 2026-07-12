namespace Callora.Contracts.Communication;

/// <summary>
/// Encoding of the audio frames flowing over one <see cref="ICallAudioStream"/>.
/// Frames are codec-encoded, not decoded PCM; consumers must check the codec
/// before interpreting payloads.
/// </summary>
/// <param name="Codec">Normalized codec name, for example "PCMU", "PCMA" or "G722".</param>
/// <param name="ClockRate">Codec clock rate in Hz, for example 8000 for G.711.</param>
public sealed record AudioFormat(string Codec, int ClockRate);
