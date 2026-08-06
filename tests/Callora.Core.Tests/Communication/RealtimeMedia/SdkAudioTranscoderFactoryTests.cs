using System;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// The adapter over the SDK's public payload-codec surface (SDK #205). It is what lets the conference
/// mix for an endpoint that cannot mix for itself: the SFU forwards encoded frames, so bridging a
/// telephone leg means decoding the other participants to PCM, mixing, and re-encoding.
/// </summary>
public sealed class SdkAudioTranscoderFactoryTests
{
    private const int TelephonyRate = 8_000;
    private const int SamplesPer20Ms = 160;

    [Fact]
    public void OpusAtTelephonyRate_RoundTripsAToneBackToAudibleAudio()
    {
        var factory = new SdkAudioTranscoderFactory();
        using var encoder = factory.Create(ConferenceAudioCodec.Opus, TelephonyRate);
        using var decoder = factory.Create(ConferenceAudioCodec.Opus, TelephonyRate);
        var tone = Tone(SamplesPer20Ms, amplitude: 8000);

        var encoded = encoder.EncodeFromPcm16(tone);
        var decoded = decoder.DecodeToPcm16(encoded);

        // Opus is lossy, so the samples differ — but a working chain compresses, and gives back audio
        // of roughly the original loudness rather than silence or noise.
        Assert.True(encoded.Length < tone.Length, "Opus payload should be smaller than raw PCM.");
        Assert.InRange(Peak(decoded), 4000, 12000);
    }

    [Fact]
    public void MuLaw_RoundTripsCloseToTheOriginalSamples()
    {
        var factory = new SdkAudioTranscoderFactory();
        using var codec = factory.Create(ConferenceAudioCodec.G711Ulaw, TelephonyRate);
        var tone = Tone(SamplesPer20Ms, amplitude: 8000);

        var decoded = codec.DecodeToPcm16(codec.EncodeFromPcm16(tone));

        // µ-law is 8 bits per sample with logarithmic quantisation: lossy, but close.
        Assert.Equal(tone.Length, decoded.Length);
        Assert.InRange(Peak(decoded), 7000, 9000);
    }

    [Fact]
    public void MuLaw_EncodesOneBytePerSample_TheFrameSizeTheTelephoneLegExpects()
    {
        var factory = new SdkAudioTranscoderFactory();
        using var codec = factory.Create(ConferenceAudioCodec.G711Ulaw, TelephonyRate);

        var encoded = codec.EncodeFromPcm16(Tone(SamplesPer20Ms, amplitude: 8000));

        // 160 bytes per 20 ms frame — what AudioFormat.G711Ulaw8k20ms declares and ICallAudioStream sends.
        Assert.Equal(SamplesPer20Ms, encoded.Length);
    }

    [Fact]
    public void FixedRateCodec_AtTheWrongRate_FailsClosed()
    {
        var factory = new SdkAudioTranscoderFactory();

        // Silently transcoding G.711 at 16 kHz would produce audio at half speed, which is far worse
        // to diagnose than a refusal at construction.
        Assert.ThrowsAny<ArgumentException>(
            () => factory.Create(ConferenceAudioCodec.G711Ulaw, 16_000));
    }

    private static byte[] Tone(int sampleCount, short amplitude)
    {
        var pcm = new byte[sampleCount * 2];
        for (var i = 0; i < sampleCount; i++)
        {
            // A 400 Hz sine at 8 kHz — a real waveform, so a lossy codec has something to preserve.
            var sample = (short)(amplitude * Math.Sin(2 * Math.PI * 400 * i / TelephonyRate));
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), sample);
        }

        return pcm;
    }

    private static int Peak(byte[] pcm)
    {
        var peak = 0;
        for (var i = 0; i + 1 < pcm.Length; i += 2)
        {
            peak = Math.Max(peak, Math.Abs(BitConverter.ToInt16(pcm, i)));
        }

        return peak;
    }
}
