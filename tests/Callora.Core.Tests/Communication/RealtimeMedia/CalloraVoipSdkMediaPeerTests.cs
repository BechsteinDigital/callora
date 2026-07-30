using System;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions.RealtimeMedia;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Callora.Plugin.Communication.Infrastructure.RealtimeMedia;
using CalloraVoipSdk.WebRtc;
using Xunit;

namespace Callora.Core.Tests.Communication.RealtimeMedia;

/// <summary>
/// The CalloraVoipSdk <see cref="IMediaPeer"/> adapter maps 1:1 onto the SDK peer: negotiation
/// (offer/answer/candidate), send-only outbound tracks, key-frame requests, connection-state projection,
/// event relaying, and handler cleanup on dispose. All neutral in — SDK out.
/// </summary>
public sealed class CalloraVoipSdkMediaPeerTests
{
    [Fact]
    public void CreateOffer_DelegatesAndTypesAsOffer()
    {
        var sdk = new FakeSdkPeerConnection { OfferSdp = "v=0-my-offer" };
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        var offer = peer.CreateOffer();

        Assert.Equal(1, sdk.CreateOfferCount);
        Assert.Equal("offer", offer.Type);
        Assert.Equal("v=0-my-offer", offer.Sdp);
    }

    [Fact]
    public async Task ApplyRemoteDescription_DelegatesAndReturnsAnswer()
    {
        var sdk = new FakeSdkPeerConnection { ReturnedDescription = "v=0-answer-sdp" };
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        var answer = await peer.ApplyRemoteDescriptionAsync(new SessionDescription("offer", "v=0-remote"));

        Assert.Equal("v=0-remote", sdk.RemoteDescription);
        Assert.NotNull(answer);
        Assert.Equal("answer", answer!.Type);
        Assert.Equal("v=0-answer-sdp", answer.Sdp);
    }

    [Fact]
    public async Task AddIceCandidate_DelegatesRawCandidate()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        await peer.AddIceCandidateAsync(new IceCandidate("candidate:1 1 udp"));

        Assert.Equal(["candidate:1 1 udp"], sdk.AddedCandidates);
    }

    [Fact]
    public async Task GatherAndStart_Delegate()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        await peer.GatherCandidatesAsync();
        await peer.StartAsync();

        Assert.Equal(1, sdk.GatherCount);
        Assert.Equal(1, sdk.StartCount);
    }

    [Fact]
    public void AddOutboundTrack_Video_AddsSendOnlyTrackWithStreamId()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        peer.AddOutboundTrack(MediaTrackKind.Video, "participant-A");

        var track = Assert.Single(sdk.VideoTracks);
        Assert.Equal(TrackDirection.SendOnly, track.Direction);
        Assert.Equal("participant-A", track.StreamId);
        Assert.Empty(sdk.AudioTracks);
    }

    [Fact]
    public void AddOutboundTrack_Audio_AddsSendOnlyTrackWithStreamId()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        peer.AddOutboundTrack(MediaTrackKind.Audio, "participant-B");

        var track = Assert.Single(sdk.AudioTracks);
        Assert.Equal(TrackDirection.SendOnly, track.Direction);
        Assert.Equal("participant-B", track.StreamId);
        Assert.Empty(sdk.VideoTracks);
    }

    [Fact]
    public async Task OutboundTrack_SendFrame_PassesPayloadAndTimestampThrough()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);
        var track = peer.AddOutboundTrack(MediaTrackKind.Video, "participant-A");

        var payload = new byte[] { 9, 8, 7 };
        await track.SendFrameAsync(new MediaFrame(payload, 42u, IsKeyFrame: true, StreamId: "participant-A"));

        var sent = Assert.Single(sdk.VideoTracks[0].SentFrames);
        Assert.Equal(payload, sent.Payload);
        Assert.Equal(42u, sent.Timestamp);
    }

    [Fact]
    public async Task OutboundTrack_SendFrame_NullTimestampBecomesZero()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);
        var track = peer.AddOutboundTrack(MediaTrackKind.Audio, "participant-B");

        await track.SendFrameAsync(new MediaFrame(new byte[] { 1 }, RtpTimestamp: null, IsKeyFrame: false, StreamId: null));

        Assert.Equal(0u, sdk.AudioTracks[0].SentFrames[0].Timestamp);
    }

    [Fact]
    public async Task RequestKeyFrame_DelegatesAndReturnsSdkResult()
    {
        var sdk = new FakeSdkPeerConnection { KeyFrameRequestResult = false };
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        var result = await peer.RequestKeyFrameAsync();

        Assert.Equal(1, sdk.KeyFrameRequestCount);
        Assert.False(result);
    }

    [Theory]
    [InlineData(PeerConnectionState.New)]
    [InlineData(PeerConnectionState.Connecting)]
    [InlineData(PeerConnectionState.Connected)]
    [InlineData(PeerConnectionState.Disconnected)]
    [InlineData(PeerConnectionState.Failed)]
    [InlineData(PeerConnectionState.Closed)]
    public void ConnectionState_MapsEverySdkState(PeerConnectionState sdkState)
    {
        var sdk = new FakeSdkPeerConnection { State = sdkState };
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        var expected = sdkState switch
        {
            PeerConnectionState.New => MediaConnectionState.New,
            PeerConnectionState.Connecting => MediaConnectionState.Connecting,
            PeerConnectionState.Connected => MediaConnectionState.Connected,
            PeerConnectionState.Disconnected => MediaConnectionState.Disconnected,
            PeerConnectionState.Failed => MediaConnectionState.Failed,
            PeerConnectionState.Closed => MediaConnectionState.Closed,
            _ => throw new ArgumentOutOfRangeException(nameof(sdkState)),
        };

        Assert.Equal(expected, peer.ConnectionState);
    }

    [Fact]
    public void ConnectionStateChanged_RelaysMappedState()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);
        MediaConnectionState? observed = null;
        peer.ConnectionStateChanged += (_, state) => observed = state;

        sdk.RaiseStateChanged(PeerConnectionState.Connected);

        Assert.Equal(MediaConnectionState.Connected, observed);
    }

    [Fact]
    public void LocalIceCandidate_RelaysAsNeutralCandidate()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);
        IceCandidate? observed = null;
        peer.LocalIceCandidateDiscovered += (_, candidate) => observed = candidate;

        sdk.RaiseLocalIceCandidate("candidate:host 1 udp");

        Assert.NotNull(observed);
        Assert.Equal("candidate:host 1 udp", observed!.Candidate);
    }

    [Fact]
    public void KeyFrameRequested_RelaysDownstreamPli()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);
        var raised = false;
        peer.KeyFrameRequested += (_, _) => raised = true;

        sdk.RaiseVideoKeyFrameRequested();

        Assert.True(raised);
    }

    [Fact]
    public async Task Dispose_DetachesHandlersAndDisposesSdkPeer()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        await peer.DisposeAsync();

        Assert.Equal(1, sdk.DisposeCount);
        Assert.False(sdk.HasStateChangedSubscribers);
        Assert.False(sdk.HasLocalIceCandidateSubscribers);
        Assert.False(sdk.HasTrackReceivedSubscribers);
        Assert.False(sdk.HasVideoKeyFrameSubscribers);
    }

    [Fact]
    public async Task Dispose_IsIdempotent()
    {
        var sdk = new FakeSdkPeerConnection();
        var peer = new CalloraVoipSdkMediaPeer(sdk);

        await peer.DisposeAsync();
        await peer.DisposeAsync();

        Assert.Equal(1, sdk.DisposeCount); // The adapter disposes the SDK peer once and is a no-op thereafter.
    }
}
