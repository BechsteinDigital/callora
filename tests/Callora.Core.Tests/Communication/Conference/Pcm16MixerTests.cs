using System;
using Callora.Plugin.Communication.Application.Conference;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// The N−1 audio mixer that lets a single-stream endpoint (a telephone) hear a conference: the SFU
/// forwards encoded streams because browsers mix locally, but a phone receives exactly one RTP stream
/// and mixes nothing, so the server sums the other participants for it.
/// </summary>
public sealed class Pcm16MixerTests
{
    [Fact]
    public void Mix_SumsTheContributions()
    {
        var destination = new byte[4];

        Pcm16Mixer.Mix(
            [Contribution([1000, -2000]), Contribution([500, 300])],
            destination);

        Assert.Equal([1500, -1700], Samples(destination));
    }

    [Fact]
    public void Mix_SaturatesInsteadOfWrapping()
    {
        var destination = new byte[4];

        // Two loud contributors exceed the PCM16 range. Wrapping would flip a loud positive peak to a
        // loud negative one — an audible crack on every overlap, the classic naive-mixer defect.
        Pcm16Mixer.Mix(
            [Contribution([30_000, -30_000]), Contribution([20_000, -20_000])],
            destination);

        Assert.Equal([short.MaxValue, short.MinValue], Samples(destination));
    }

    [Fact]
    public void Mix_WithZeroGain_SilencesThatContributionOnly()
    {
        var destination = new byte[2];

        // Server-side mute: the muted leg's audio never reaches the mix, whatever its device does.
        Pcm16Mixer.Mix(
            [Contribution([9000], gain: 0f), Contribution([1000])],
            destination);

        Assert.Equal([1000], Samples(destination));
    }

    [Fact]
    public void Mix_HalfGain_ScalesTheContribution()
    {
        var destination = new byte[2];

        Pcm16Mixer.Mix([Contribution([1000], gain: 0.5f)], destination);

        Assert.Equal([500], Samples(destination));
    }

    [Fact]
    public void Mix_ShortContribution_FillsTheRemainderWithTheOthersOnly()
    {
        var destination = new byte[6];

        // A contributor whose frame has not fully arrived yet (jitter) must not truncate the mix or
        // drag the other participants' audio down with it.
        Pcm16Mixer.Mix(
            [Contribution([100]), Contribution([200, 300, 400])],
            destination);

        Assert.Equal([300, 300, 400], Samples(destination));
    }

    [Fact]
    public void Mix_WithoutContributions_WritesSilence()
    {
        var destination = new byte[4];

        Pcm16Mixer.Mix([], destination);

        Assert.Equal([0, 0], Samples(destination));
    }

    [Fact]
    public void Mix_OverwritesEveryStaleSampleInTheDestination()
    {
        // Pooled buffers carry the previous frame. A mixer that only writes where it has input would
        // replay that frame's audio into the gaps — the caller must be able to hand over any buffer.
        var destination = Pcm([9999, 9999, 9999]);

        Pcm16Mixer.Mix([Contribution([50])], destination);

        Assert.Equal([50, 0, 0], Samples(destination));
    }

    private static Pcm16Contribution Contribution(short[] samples, float gain = 1f) =>
        new(Pcm(samples), gain);

    private static byte[] Pcm(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static short[] Samples(byte[] pcm)
    {
        var samples = new short[pcm.Length / 2];
        Buffer.BlockCopy(pcm, 0, samples, 0, pcm.Length);
        return samples;
    }
}
