namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Beschreibt das Frame-Format eines <see cref="ICallAudioStream"/>.</summary>
/// <param name="Codec">Der Audio-Codec.</param>
/// <param name="SampleRateHz">Abtastrate in Hz.</param>
/// <param name="FrameMilliseconds">Frame-Länge in Millisekunden.</param>
public sealed record AudioFormat(AudioCodec Codec, int SampleRateHz, int FrameMilliseconds)
{
    /// <summary>SIP/PSTN-Standard: G.711 µ-law, 8 kHz, 20-ms-Frames.</summary>
    public static AudioFormat G711Ulaw8k20ms { get; } = new(AudioCodec.G711Ulaw, 8000, 20);
}
