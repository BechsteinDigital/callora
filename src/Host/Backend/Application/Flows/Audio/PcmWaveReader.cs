using System.Buffers.Binary;

namespace Callora.Host.Backend.Application.Flows.Audio;

/// <summary>
/// Minimal RIFF/WAV reader for announcement files: accepts PCM 16-bit mono
/// and returns the samples plus sample rate. Anything else is rejected with a
/// clear message so operators know how to export their audio.
/// </summary>
public static class PcmWaveReader
{
    public static (short[] Samples, int SampleRate) Read(byte[] wavBytes)
    {
        ArgumentNullException.ThrowIfNull(wavBytes);
        if (wavBytes.Length < 44 ||
            !wavBytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) ||
            !wavBytes.AsSpan(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidOperationException("Announcement audio must be a RIFF/WAVE file.");
        }

        var offset = 12;
        short channels = 0;
        var sampleRate = 0;
        short bitsPerSample = 0;

        while (offset + 8 <= wavBytes.Length)
        {
            var chunkId = wavBytes.AsSpan(offset, 4);
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(wavBytes.AsSpan(offset + 4, 4));
            var dataOffset = offset + 8;

            if (chunkId.SequenceEqual("fmt "u8))
            {
                var audioFormat = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.AsSpan(dataOffset, 2));
                channels = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.AsSpan(dataOffset + 2, 2));
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(wavBytes.AsSpan(dataOffset + 4, 4));
                bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.AsSpan(dataOffset + 14, 2));

                if (audioFormat != 1 || channels != 1 || bitsPerSample != 16)
                {
                    throw new InvalidOperationException(
                        "Announcement audio must be PCM 16-bit mono WAV (got " +
                        $"format={audioFormat}, channels={channels}, bits={bitsPerSample}).");
                }
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                if (sampleRate == 0)
                {
                    throw new InvalidOperationException("WAV data chunk appeared before the fmt chunk.");
                }

                var byteCount = Math.Min(chunkSize, wavBytes.Length - dataOffset);
                var samples = new short[byteCount / 2];
                for (var i = 0; i < samples.Length; i++)
                {
                    samples[i] = BinaryPrimitives.ReadInt16LittleEndian(wavBytes.AsSpan(dataOffset + i * 2, 2));
                }

                return (samples, sampleRate);
            }

            offset = dataOffset + chunkSize + (chunkSize % 2);
        }

        throw new InvalidOperationException("WAV file contains no data chunk.");
    }
}
