using System.Buffers.Binary;
using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Flows.Audio;
using Callora.Host.Backend.Tests.Support;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Flows;

public sealed class AnnouncementAudioTests
{
    [Fact]
    public void G711_EncodesKnownReferenceValues()
    {
        // ITU-T reference: linear 0 → A-law 0xD5, µ-law 0xFF.
        Assert.Equal(0xD5, G711Codec.EncodeALaw(0));
        Assert.Equal(0xFF, G711Codec.EncodeMuLaw(0));
        // Sign handling: positive and negative extremes differ in the sign bit.
        Assert.NotEqual(G711Codec.EncodeALaw(short.MaxValue), G711Codec.EncodeALaw(short.MinValue));
        Assert.NotEqual(G711Codec.EncodeMuLaw(short.MaxValue), G711Codec.EncodeMuLaw(short.MinValue));
    }

    [Fact]
    public void WaveReader_ParsesPcm16MonoWav()
    {
        var wav = BuildWav(sampleRate: 8000, samples: [0, 1000, -1000, short.MaxValue]);

        var (samples, sampleRate) = PcmWaveReader.Read(wav);

        Assert.Equal(8000, sampleRate);
        Assert.Equal([0, 1000, -1000, short.MaxValue], samples);
    }

    [Fact]
    public void WaveReader_RejectsNonWavContent()
    {
        Assert.Throws<InvalidOperationException>(() => PcmWaveReader.Read([1, 2, 3, 4]));
    }

    [Fact]
    public async Task Streamer_SendsPacedG711Frames_IntoCallAudioStream()
    {
        // 8000 Hz → 160 Samples pro 20-ms-Frame; 400 Samples ergeben 3 Frames.
        var wav = BuildWav(8000, new short[400]);
        var stream = new RecordingCallAudioStream(new AudioFormat("PCMA", 8000));

        await AnnouncementStreamer.StreamAsync(stream, wav);

        Assert.Equal(3, stream.SentFrames.Count);
        Assert.Equal(160, stream.SentFrames[0].Payload.Length);
        Assert.Equal(80, stream.SentFrames[2].Payload.Length);
        Assert.All(stream.SentFrames, frame => Assert.Equal(TimeSpan.FromMilliseconds(20), frame.Duration));
        // Stille (0) als A-law ist 0xD5 in jedem Byte.
        Assert.All(stream.SentFrames[0].Payload.ToArray(), b => Assert.Equal(0xD5, b));
    }

    [Fact]
    public async Task Streamer_RejectsUnsupportedCodec_AndSampleRateMismatch()
    {
        var wav = BuildWav(8000, new short[160]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AnnouncementStreamer.StreamAsync(new RecordingCallAudioStream(new AudioFormat("G722", 16000)), wav));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AnnouncementStreamer.StreamAsync(new RecordingCallAudioStream(new AudioFormat("PCMU", 16000)), wav));
    }

    private static byte[] BuildWav(int sampleRate, short[] samples)
    {
        var dataSize = samples.Length * 2;
        var bytes = new byte[44 + dataSize];
        "RIFF"u8.CopyTo(bytes);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 36 + dataSize);
        "WAVE"u8.CopyTo(bytes.AsSpan(8));
        "fmt "u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(28), sampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(34), 16);
        "data"u8.CopyTo(bytes.AsSpan(36));
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), dataSize);
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(44 + i * 2), samples[i]);
        }

        return bytes;
    }
}
