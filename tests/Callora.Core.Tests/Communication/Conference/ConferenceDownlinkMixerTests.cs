using System;
using System.Linq;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// What a telephone leg needs in order to hear a conference: the SFU forwards one encoded stream per
/// participant because browsers mix locally, but a phone receives a single stream. This decodes each
/// participant, mixes them, and re-encodes to the phone's codec — once per outbound frame.
/// </summary>
public sealed class ConferenceDownlinkMixerTests
{
    private const int TelephonyRate = 8_000;
    private const int SamplesPer20Ms = 160;

    [Fact]
    public void NextFrame_WithoutSources_IsSilence()
    {
        using var mixer = NewMixer();

        var frame = mixer.NextFrame();

        // A single participant in a room hears silence, not stale buffer content or noise.
        Assert.Equal(SamplesPer20Ms, frame.Length);
        Assert.InRange(DecodedPeak(frame), 0, 64);
    }

    [Fact]
    public void NextFrame_MixesTheSourcesThatPushed()
    {
        using var mixer = NewMixer();
        mixer.AddSource("alice");
        mixer.AddSource("bob");
        mixer.Push("alice", OpusFrame(amplitude: 6000));
        mixer.Push("bob", OpusFrame(amplitude: 6000));

        var frame = mixer.NextFrame();

        // Both contributions arrive: the sum is audibly louder than either alone would be.
        Assert.InRange(DecodedPeak(frame), 7000, 16000);
    }

    [Fact]
    public void NextFrame_ConsumesEachPushedFrameOnce()
    {
        using var mixer = NewMixer();
        mixer.AddSource("alice");
        mixer.Push("alice", OpusFrame(amplitude: 8000));

        mixer.NextFrame();
        var second = mixer.NextFrame();

        // Replaying the last frame would stutter the speaker's audio on every gap in their stream.
        Assert.InRange(DecodedPeak(second), 0, 64);
    }

    [Fact]
    public void NextFrame_AfterRemoveSource_DropsThatParticipant()
    {
        using var mixer = NewMixer();
        mixer.AddSource("alice");
        mixer.Push("alice", OpusFrame(amplitude: 8000));
        mixer.RemoveSource("alice");

        Assert.InRange(DecodedPeak(mixer.NextFrame()), 0, 64);
    }

    [Fact]
    public void Push_FromAnUnknownSource_IsIgnored()
    {
        using var mixer = NewMixer();

        // A frame in flight when a participant leaves must not resurrect them into the mix.
        mixer.Push("ghost", OpusFrame(amplitude: 8000));

        Assert.InRange(DecodedPeak(mixer.NextFrame()), 0, 64);
    }

    [Fact]
    public void NextFrame_ProducesTheFrameSizeTheTelephoneLegExpects()
    {
        using var mixer = NewMixer();
        mixer.AddSource("alice");
        mixer.Push("alice", OpusFrame(amplitude: 3000));

        // 160 bytes of µ-law per 20 ms — what AudioFormat.G711Ulaw8k20ms declares.
        Assert.Equal(SamplesPer20Ms, mixer.NextFrame().Length);
    }

    private static ConferenceDownlinkMixer NewMixer() =>
        new(new SdkAudioTranscoderFactory(), ConferenceAudioCodec.Opus, ConferenceAudioCodec.G711Ulaw, TelephonyRate, SamplesPer20Ms);

    private static byte[] OpusFrame(short amplitude)
    {
        using var encoder = new SdkAudioTranscoderFactory().Create(ConferenceAudioCodec.Opus, TelephonyRate);
        return encoder.EncodeFromPcm16(Tone(amplitude));
    }

    private static byte[] Tone(short amplitude)
    {
        var pcm = new byte[SamplesPer20Ms * 2];
        for (var i = 0; i < SamplesPer20Ms; i++)
        {
            var sample = (short)(amplitude * Math.Sin(2 * Math.PI * 400 * i / TelephonyRate));
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), sample);
        }

        return pcm;
    }

    private static int DecodedPeak(byte[] muLaw)
    {
        using var decoder = new SdkAudioTranscoderFactory().Create(ConferenceAudioCodec.G711Ulaw, TelephonyRate);
        var pcm = decoder.DecodeToPcm16(muLaw);
        return Enumerable.Range(0, pcm.Length / 2).Max(i => Math.Abs(BitConverter.ToInt16(pcm, i * 2)));
    }
}
