namespace Callora.Host.Backend.Application.Flows.Audio;

/// <summary>
/// G.711 A-law/µ-law encoding of 16-bit PCM samples (ITU-T G.711) — used to
/// stream announcement audio into calls negotiated with PCMA/PCMU.
/// </summary>
public static class G711Codec
{
    public static byte EncodeALaw(short sample)
    {
        var sign = (sample & 0x8000) == 0 ? 0xD5 : 0x55;
        var magnitude = sample < 0 ? (sample == short.MinValue ? short.MaxValue : -sample) : sample;

        byte compressed;
        if (magnitude < 256)
        {
            compressed = (byte)(magnitude >> 4);
        }
        else
        {
            var exponent = 7;
            for (var mask = 0x4000; (magnitude & mask) == 0 && exponent > 0; exponent--, mask >>= 1)
            {
            }

            var mantissa = (magnitude >> (exponent + 3)) & 0x0F;
            compressed = (byte)((exponent << 4) | mantissa);
        }

        return (byte)(compressed ^ sign);
    }

    public static byte EncodeMuLaw(short sample)
    {
        const int Bias = 0x84;
        const int Clip = 32635;

        var sign = (sample >> 8) & 0x80;
        var magnitude = sample < 0 ? (sample == short.MinValue ? short.MaxValue : -sample) : (int)sample;
        magnitude = Math.Min(magnitude + Bias, Clip + Bias);

        var exponent = 7;
        for (var mask = 0x4000; (magnitude & mask) == 0 && exponent > 0; exponent--, mask >>= 1)
        {
        }

        var mantissa = (magnitude >> (exponent + 3)) & 0x0F;
        return (byte)(~(sign | (exponent << 4) | mantissa));
    }

    public static byte[] Encode(ReadOnlySpan<short> samples, bool aLaw)
    {
        var encoded = new byte[samples.Length];
        for (var i = 0; i < samples.Length; i++)
        {
            encoded[i] = aLaw ? EncodeALaw(samples[i]) : EncodeMuLaw(samples[i]);
        }

        return encoded;
    }
}
