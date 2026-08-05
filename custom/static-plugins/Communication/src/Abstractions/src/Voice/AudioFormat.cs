namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Beschreibt das Frame-Format eines <see cref="ICallAudioStream"/>.</summary>
/// <param name="Codec">Der Audio-Codec.</param>
/// <param name="SampleRateHz">Abtastrate in Hz.</param>
/// <param name="FrameMilliseconds">Frame-Länge in Millisekunden.</param>
public sealed record AudioFormat(AudioCodec Codec, int SampleRateHz, int FrameMilliseconds)
{
    /// <summary>SIP/PSTN-Standard: G.711 µ-law, 8 kHz, 20-ms-Frames.</summary>
    public static AudioFormat G711Ulaw8k20ms { get; } = new(AudioCodec.G711Ulaw, 8000, 20);

    /// <summary>
    /// Exakte Frame-Größe in Bytes. G.711 kodiert ein Sample je Byte, also
    /// <c>SampleRateHz × FrameMilliseconds / 1000</c> — für 8 kHz/20 ms sind das 160 Bytes.
    /// Das ausgehandelte Format wird damit prüfbar: eingehende Frames dürfen genau
    /// diese Größe haben, statt beliebig groß zu sein (#108).
    /// </summary>
    public int BytesPerFrame => SampleRateHz * FrameMilliseconds / 1000;
}
