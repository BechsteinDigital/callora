using System.Buffers.Binary;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Sums PCM16 contributions into one stream — what a single-stream endpoint needs in order to hear a
/// conference the SFU otherwise forwards as N separate streams.
/// </summary>
internal static class Pcm16Mixer
{
    /// <summary>
    /// Mixes <paramref name="contributions"/> into <paramref name="destination"/> as PCM16
    /// little-endian.
    /// </summary>
    /// <param name="contributions">The contributors' samples and their gains.</param>
    /// <param name="destination">
    /// The frame to fill; its length sets the mixed frame's length. Written in full — samples no
    /// contributor covers become silence.
    /// </param>
    public static void Mix(ReadOnlySpan<Pcm16Contribution> contributions, Span<byte> destination)
    {
        var sampleCount = destination.Length / 2;

        for (var i = 0; i < sampleCount; i++)
        {
            var sum = 0;

            foreach (var contribution in contributions)
            {
                var samples = contribution.Pcm16.Span;
                var offset = i * 2;
                if (offset + 1 >= samples.Length)
                {
                    continue;
                }

                var sample = BinaryPrimitives.ReadInt16LittleEndian(samples[offset..]);
                sum += (int)MathF.Round(sample * contribution.Gain);
            }

            // Saturate rather than wrap: summing two loud contributors overflows PCM16, and a wrap
            // turns a loud positive peak into a loud negative one — an audible crack on every overlap.
            BinaryPrimitives.WriteInt16LittleEndian(
                destination[(i * 2)..],
                (short)Math.Clamp(sum, short.MinValue, short.MaxValue));
        }
    }
}
