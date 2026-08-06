using System;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// The other direction of the telephone bridge: what the phone says has to reach the conference as an
/// ordinary participant stream, so the SFU can forward it to every browser unchanged.
/// </summary>
public sealed class ConferenceUplinkEncoderTests
{
    private const int TelephonyRate = 8_000;
    private const int SamplesPer20Ms = 160;

    // Opus always stamps RTP at 48 kHz regardless of the rate it codes at (RFC 7587 §4.1), so a 20 ms
    // frame advances the timestamp by 960 — not by the 160 samples the 8 kHz PCM actually holds.
    private const uint OpusTicksPer20Ms = 960;

    [Fact]
    public void Encode_TurnsATelephoneFrameIntoAConferencePayload()
    {
        using var uplink = NewUplink();

        var frame = uplink.Encode(MuLawTone(amplitude: 8000));

        Assert.NotNull(frame);
        Assert.NotEmpty(frame!.Value.Payload);
    }

    [Fact]
    public void Encode_AdvancesTheRtpTimestampOnTheOpusClock_NotThePcmRate()
    {
        using var uplink = NewUplink();

        var first = uplink.Encode(MuLawTone(8000))!.Value;
        var second = uplink.Encode(MuLawTone(8000))!.Value;

        // Advancing by 160 (the PCM sample count) would make the conference play the phone leg six
        // times too fast, and drift it against every other participant's A/V sync.
        Assert.Equal(OpusTicksPer20Ms, second.RtpTimestamp - first.RtpTimestamp);
    }

    [Fact]
    public void Encode_WhileMuted_ProducesNothing()
    {
        using var uplink = NewUplink();
        uplink.IsMuted = true;

        // Server-side mute: the audio never leaves this leg, whatever the handset does.
        Assert.Null(uplink.Encode(MuLawTone(8000)));
    }

    [Fact]
    public void Encode_AfterUnmute_ContinuesTheTimestampWithoutJumpingBack()
    {
        using var uplink = NewUplink();
        var before = uplink.Encode(MuLawTone(8000))!.Value;

        uplink.IsMuted = true;
        uplink.Encode(MuLawTone(8000));
        uplink.Encode(MuLawTone(8000));
        uplink.IsMuted = false;
        var after = uplink.Encode(MuLawTone(8000))!.Value;

        // The clock keeps running while muted: the receiver reads the gap as silence. Freezing it
        // would make the resumed audio look like it belongs three frames in the past.
        Assert.Equal(3 * OpusTicksPer20Ms, after.RtpTimestamp - before.RtpTimestamp);
    }

    private static ConferenceUplinkEncoder NewUplink() =>
        new(new SdkAudioTranscoderFactory(), ConferenceAudioCodec.G711Ulaw, ConferenceAudioCodec.Opus, TelephonyRate, SamplesPer20Ms);

    private static byte[] MuLawTone(short amplitude)
    {
        using var encoder = new SdkAudioTranscoderFactory().Create(ConferenceAudioCodec.G711Ulaw, TelephonyRate);
        var pcm = new byte[SamplesPer20Ms * 2];
        for (var i = 0; i < SamplesPer20Ms; i++)
        {
            var sample = (short)(amplitude * Math.Sin(2 * Math.PI * 400 * i / TelephonyRate));
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 2), sample);
        }

        return encoder.EncodeFromPcm16(pcm);
    }
}
