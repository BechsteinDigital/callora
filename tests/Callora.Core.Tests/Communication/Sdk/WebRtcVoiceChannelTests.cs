using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.WebRtc;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The WebRTC channel shell (S2): a foundation <see cref="IVoiceChannel"/> over an
/// <see cref="IWebRtcClient"/>. It advertises the voice capability, reports health from client liveness,
/// rejects server-initiated outbound placement (WebRTC is browser-initiated via signalling), and offers
/// the <c>TrackIncomingCall</c> seam the signalling transport (S3) drives.
/// </summary>
public sealed class WebRtcVoiceChannelTests
{
    [Fact]
    public void Identity_AndCapabilities_CoverVoiceAndWebRtc()
    {
        var channel = NewChannel();

        Assert.Equal("webrtc-1", channel.ChannelId);
        Assert.Equal("Browser Voice", channel.DisplayName);
        Assert.Equal("communication", channel.PluginId);
        // The channel is the WebRTC surface, so it publishes that capability too — the manifest
        // declared it conditionally while nothing reported it, leaving it unsatisfiable (#115).
        Assert.Equal(
            [CommunicationCapabilities.Voice, CommunicationCapabilities.WebRtc],
            channel.Capabilities);
    }

    [Fact]
    public void Health_IsUpWhileClientAlive()
    {
        var channel = NewChannel();

        Assert.Equal(ChannelHealth.Up, channel.Health);
    }

    [Fact]
    public async Task PlaceCallAsync_Throws()
    {
        var channel = NewChannel();

        await Assert.ThrowsAsync<NotSupportedException>(
            () => channel.PlaceCallAsync(new CallTarget("webrtc:browser-1")));
    }

    [Fact]
    public void TrackIncomingCall_RaisesIncomingWithInboundWebRtcCall()
    {
        var channel = NewChannel();
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var target = new CallTarget("webrtc:browser-1", "Alice");
        IncomingCallEventArgs? raised = null;
        channel.IncomingCall += (_, e) => raised = e;

        var returned = channel.TrackIncomingCall(peer, "call-42", target);

        Assert.NotNull(raised);
        Assert.Same(returned, raised!.Call);
        Assert.IsType<WebRtcCall>(raised.Call);
        Assert.Equal("call-42", raised.Call.CallId);
        Assert.Equal(CallDirection.Inbound, raised.Call.Direction);
        Assert.Equal("webrtc:browser-1", raised.Call.Target.Value);
        Assert.Equal("Alice", raised.Call.Target.DisplayName);
        Assert.Equal(CallState.Connected, raised.Call.State);
    }

    [Fact]
    public void TrackIncomingCall_WithoutSubscriber_StillReturnsCall()
    {
        var channel = NewChannel();
        var peer = new FakePeerConnection { State = PeerConnectionState.Connecting };

        var call = channel.TrackIncomingCall(peer, "call-1", new CallTarget("webrtc:browser-1"));

        Assert.Equal("call-1", call.CallId);
        Assert.Equal(CallState.Connecting, call.State);
    }

    private static WebRtcVoiceChannel NewChannel() =>
        new("webrtc-1", "Browser Voice", "communication", new FakeWebRtcClient());
}
