using System;
using System.Linq;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// A call taking part in a conference as an ordinary participant. Everything the forwarding path does
/// to a browser peer it does to this too — the difference is only what sits behind the members: a mix
/// and a transcoder instead of a WebRTC transport.
/// </summary>
public sealed class CallConferenceEndpointTests
{
    private const int TelephonyRate = 8_000;
    private const int SamplesPer20Ms = 160;

    [Fact]
    public void AnOutboundAudioTrack_FeedsThatSourceIntoTheMix()
    {
        using var fixture = NewFixture();

        var track = fixture.Endpoint.AddOutboundTrack(MediaTrackKind.Audio, "alice");
        track.SendFrameAsync(new MediaFrame(OpusFrame(8000), RtpTimestamp: 0, IsKeyFrame: false, StreamId: "alice"));

        // The router thinks it is forwarding a frame to a peer; what actually happens is that alice
        // becomes audible in the single stream this endpoint receives.
        Assert.InRange(DecodedPeak(fixture.Mixer.NextFrame()), 4000, 12000);
    }

    [Fact]
    public void AnOutboundVideoTrack_AcceptsFramesAndDropsThem()
    {
        using var fixture = NewFixture();

        var track = fixture.Endpoint.AddOutboundTrack(MediaTrackKind.Video, "alice");

        // The router adds a video track per source unconditionally. Refusing it would make the phone a
        // special case in the forwarding path — the whole point is that it is not one.
        Assert.NotNull(track);
        track.SendFrameAsync(new MediaFrame(new byte[] { 1, 2, 3 }, RtpTimestamp: 0, IsKeyFrame: true, StreamId: "alice"));
    }

    [Fact]
    public void TheEndpoint_SurfacesOneInboundAudioTrackCarryingWhatTheCallerSays()
    {
        using var fixture = NewFixture();
        IRemoteMediaTrack? surfaced = null;
        fixture.Endpoint.RemoteTrackReceived += (_, track) => surfaced = track;

        fixture.Endpoint.Start();
        MediaFrame? forwarded = null;
        surfaced!.FrameReceived += (_, frame) => forwarded = frame;
        fixture.Endpoint.PushFromCall(MuLawTone(8000));

        Assert.Equal(MediaTrackKind.Audio, surfaced.Kind);
        Assert.NotNull(forwarded);
        Assert.NotEmpty(forwarded!.Value.Payload.ToArray());
    }

    [Fact]
    public void TheEndpoint_IsConnectedSoTheRouterForwardsToIt()
    {
        using var fixture = NewFixture();

        // The router's readiness gate skips a consumer that is not Connected; a call that is up has no
        // handshake left to wait for.
        Assert.Equal(MediaConnectionState.Connected, fixture.Endpoint.ConnectionState);
    }

    [Fact]
    public async Task RequestKeyFrame_IsRefusedBecauseThereIsNoVideo()
    {
        using var fixture = NewFixture();

        Assert.False(await fixture.Endpoint.RequestKeyFrameAsync());
    }

    [Fact]
    public async Task Renegotiate_CostsTheEndpointNothing()
    {
        using var fixture = NewFixture();

        // A topology change re-offers to a browser. There is no SDP on this leg, so the honest answer
        // is that nothing needs doing — not an error, which would surface on every join and leave.
        await fixture.Endpoint.RenegotiateAsync();
    }

    [Fact]
    public void Muting_KeepsWhatTheCallerSaysOutOfTheConference()
    {
        using var fixture = NewFixture();
        IRemoteMediaTrack? surfaced = null;
        fixture.Endpoint.RemoteTrackReceived += (_, track) => surfaced = track;
        fixture.Endpoint.Start();
        var forwarded = 0;
        surfaced!.FrameReceived += (_, _) => forwarded++;

        fixture.Endpoint.IsMuted = true;
        fixture.Endpoint.PushFromCall(MuLawTone(8000));

        Assert.Equal(0, forwarded);
    }

    private static EndpointFixture NewFixture()
    {
        var transcoders = new SdkAudioTranscoderFactory();
        var mixer = new ConferenceDownlinkMixer(
            transcoders, ConferenceAudioCodec.Opus, ConferenceAudioCodec.G711Ulaw, TelephonyRate, SamplesPer20Ms);
        var endpoint = new CallConferenceEndpoint(
            mixer, transcoders, ConferenceAudioCodec.G711Ulaw, ConferenceAudioCodec.Opus, TelephonyRate, SamplesPer20Ms);
        return new EndpointFixture(mixer, endpoint);
    }

    private static byte[] OpusFrame(short amplitude)
    {
        using var encoder = new SdkAudioTranscoderFactory().Create(ConferenceAudioCodec.Opus, TelephonyRate);
        return encoder.EncodeFromPcm16(Pcm(amplitude));
    }

    private static byte[] MuLawTone(short amplitude)
    {
        using var encoder = new SdkAudioTranscoderFactory().Create(ConferenceAudioCodec.G711Ulaw, TelephonyRate);
        return encoder.EncodeFromPcm16(Pcm(amplitude));
    }

    private static byte[] Pcm(short amplitude)
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

    private sealed record EndpointFixture(ConferenceDownlinkMixer Mixer, CallConferenceEndpoint Endpoint) : IDisposable
    {
        public void Dispose()
        {
            Endpoint.Dispose();
            Mixer.Dispose();
        }
    }
}
