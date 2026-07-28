using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.WebRtc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The WebRTC signalling transport (S3): the handler is the offerer. It creates a peer, sends the
/// <c>offer</c> and trickled local candidates, applies the browser's <c>answer</c> (then starts the
/// transport) and remote candidates, attaches the connected peer to its channel exactly once, ignores
/// malformed frames, and disposes an unattached peer on close.
/// </summary>
public sealed class WebRtcSignalingWebSocketHandlerTests
{
    [Fact]
    public async Task Handshake_SendsOfferThenLocalCandidate()
    {
        var peer = new FakePeerConnection { OfferSdp = "v=0-my-offer", CandidateOnOffer = "candidate:host-1" };
        var (handler, session) = NewHandler(peer);
        // Only the offer/candidate handshake — no inbound frames, socket closes immediately after.
        var socket = new FakeSignalingWebSocket();

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        Assert.True(peer.OfferCreated);
        Assert.Equal(["offer", "candidate"], socket.SentTypes);
        Assert.Contains("v=0-my-offer", socket.Sent[0]);
        Assert.Contains("candidate:host-1", socket.Sent[1]);
    }

    [Fact]
    public async Task IncomingAnswer_SetsRemoteDescriptionThenStarts()
    {
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer);
        var socket = new FakeSignalingWebSocket(Frame("answer", sdp: "v=0-browser-answer"));

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        Assert.Equal("v=0-browser-answer", peer.RemoteDescription);
        Assert.True(peer.Started);
    }

    [Fact]
    public async Task IncomingCandidate_IsAddedToPeer()
    {
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer);
        var socket = new FakeSignalingWebSocket(Frame("candidate", candidate: "candidate:browser-9"));

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        Assert.Equal(["candidate:browser-9"], peer.AddedCandidates);
    }

    [Fact]
    public async Task PeerConnected_RaisesIncomingCallOnceAsInboundWebRtcCall()
    {
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer);
        var raised = new List<IncomingCallEventArgs>();
        session.Channel.IncomingCall += (_, e) => raised.Add(e);
        // Applying the answer drives the peer to Connected mid-handling (twice to prove the once-guard).
        var socket = new FakeSignalingWebSocket(
            Frame("answer", sdp: "v=0-answer"),
            Frame("candidate", candidate: "candidate:late"));
        peer.OnRemoteDescriptionApplied = () =>
        {
            peer.RaiseStateChanged(PeerConnectionState.Connected);
            peer.RaiseStateChanged(PeerConnectionState.Connected);
        };

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        var call = Assert.Single(raised).Call;
        Assert.IsType<WebRtcCall>(call);
        Assert.Equal("call-webrtc-1", call.CallId);
        Assert.Equal(CallDirection.Inbound, call.Direction);
        Assert.Equal("webrtc:browser-x", call.Target.Value);
    }

    [Fact]
    public async Task MalformedFrame_IsIgnored_HandlerStaysStable()
    {
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer);
        var socket = new FakeSignalingWebSocket(
            "{ this is not json",
            "[]",                                   // valid JSON, but not a signalling object
            Frame("candidate", candidate: "candidate:good"));

        // Must not throw despite the two junk frames before the good one.
        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        Assert.Equal(["candidate:good"], peer.AddedCandidates);
    }

    [Fact]
    public async Task SocketClosesBeforeConnected_DisposesPeer()
    {
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer);
        var socket = new FakeSignalingWebSocket(); // closes immediately, peer never connects

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        Assert.Equal(1, peer.DisposeCount);
    }

    [Fact]
    public async Task PeerConnected_DoesNotDisposePeerOnClose()
    {
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer);
        var socket = new FakeSignalingWebSocket(Frame("answer", sdp: "v=0-answer"));
        peer.OnRemoteDescriptionApplied = () => peer.RaiseStateChanged(PeerConnectionState.Connected);

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        // A WebRtcCall now owns the peer's lifetime; the negotiation must not dispose it.
        Assert.Equal(0, peer.DisposeCount);
    }

    [Fact]
    public async Task DisposeAfterAttach_DoesNotDisposePeer_AndAttachesOnce()
    {
        // Drives the negotiation directly to pin the ownership interlock: once the connected-attach path
        // claims the peer, a later DisposeAsync must lose the claim and leave the peer alone.
        var peer = new FakePeerConnection();
        var client = new RecordingWebRtcClient(peer);
        var channel = new WebRtcVoiceChannel("webrtc-1", "Browser Voice", CommunicationPlugin.Id, client);
        var session = new WebRtcSignalingSession(client, channel, "call-1", new CallTarget("webrtc:browser-x"));
        var incoming = new List<IncomingCallEventArgs>();
        channel.IncomingCall += (_, e) => incoming.Add(e);

        var socket = new FakeSignalingWebSocket();
        using var signalingChannel = new WebRtcSignalingChannel(socket);
        var negotiation = new WebRtcSignalingNegotiation(
            peer, session, signalingChannel, NullLogger<WebRtcSignalingWebSocketHandler>.Instance);

        await negotiation.StartAsync(CancellationToken.None);
        peer.RaiseStateChanged(PeerConnectionState.Connected); // attach wins the claim
        await negotiation.DisposeAsync();                      // dispose must lose the claim

        Assert.Equal(0, peer.DisposeCount);
        Assert.Single(incoming);
    }

    [Fact]
    public async Task DisposeWithoutAttach_DisposesPeerExactlyOnce()
    {
        var peer = new FakePeerConnection();
        var client = new RecordingWebRtcClient(peer);
        var channel = new WebRtcVoiceChannel("webrtc-1", "Browser Voice", CommunicationPlugin.Id, client);
        var session = new WebRtcSignalingSession(client, channel, "call-1", new CallTarget("webrtc:browser-x"));

        var socket = new FakeSignalingWebSocket();
        using var signalingChannel = new WebRtcSignalingChannel(socket);
        var negotiation = new WebRtcSignalingNegotiation(
            peer, session, signalingChannel, NullLogger<WebRtcSignalingWebSocketHandler>.Instance);

        await negotiation.StartAsync(CancellationToken.None);
        await negotiation.DisposeAsync(); // never connected — dispose wins the claim
        await negotiation.DisposeAsync(); // idempotent

        Assert.Equal(1, peer.DisposeCount);
    }

    [Fact]
    public async Task AnswerDeadline_NoBrowserAnswer_DisposesPeer_WithoutHangingLoop()
    {
        // Arrange: socket blocks after the queue drains (simulates a browser that never replies).
        // A very short deadline is injected so the test does not take 30 s in CI.
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer, answerDeadline: TimeSpan.FromMilliseconds(50));
        var socket = new FakeSignalingWebSocket { BlockAfterQueueDrained = true };

        // Act: handler must return (not hang) and dispose the peer once the deadline fires.
        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        // Assert: peer was disposed (deadline path — no WebRtcCall took it over).
        Assert.Equal(1, peer.DisposeCount);
    }

    [Fact]
    public async Task AnswerDeadline_AfterAnswer_DoesNotCapSocket()
    {
        // After the answer, the deadline is disarmed. The socket stays open until the client closes it.
        // Verified by: handler completes without error and the peer is NOT disposed (a WebRtcCall owns it).
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer, answerDeadline: TimeSpan.FromMilliseconds(50));
        var socket = new FakeSignalingWebSocket(Frame("answer", sdp: "v=0-answer"));
        peer.OnRemoteDescriptionApplied = () => peer.RaiseStateChanged(PeerConnectionState.Connected);

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        // The deadline was disarmed; normal close path — peer is not disposed because a WebRtcCall owns it.
        Assert.Equal(0, peer.DisposeCount);
    }

    [Fact]
    public async Task StartAsync_GathersCandidates_AfterOffer_BeforeStart()
    {
        // GatherCandidatesAsync must be called in StartAsync (after the offer, so buffered candidates
        // already flushed) and before peer.StartAsync (which the answer handler triggers). Order: offer →
        // gather → start.
        var peer = new FakePeerConnection();
        var (handler, session) = NewHandler(peer);
        var socket = new FakeSignalingWebSocket(Frame("answer", sdp: "v=0-browser-answer"));

        await handler.HandleAsync(Connection(socket, session), CancellationToken.None);

        Assert.True(peer.CandidatesGathered, "GatherCandidatesAsync must be called to enable STUN/NAT traversal.");
        var gatherIdx = peer.CallOrder.IndexOf("gather");
        var offerIdx = peer.CallOrder.IndexOf("offer");
        var startIdx = peer.CallOrder.IndexOf("start");
        Assert.True(offerIdx >= 0 && gatherIdx > offerIdx, "gather must come after offer in call order.");
        Assert.True(startIdx < 0 || gatherIdx < startIdx, "gather must come before start (transport) in call order.");
    }

    [Fact]
    public async Task NoSessionResolved_ClosesWithoutCreatingPeer()
    {
        var resolver = new FakeSessionResolver(session: null);
        var handler = new WebRtcSignalingWebSocketHandler(resolver, NullLogger<WebRtcSignalingWebSocketHandler>.Instance);
        var socket = new FakeSignalingWebSocket();
        var request = new HostWebSocketConnectRequest(
            CommunicationPlugin.Id, "webrtc/tok", new Dictionary<string, string>(),
            new Dictionary<string, string[]>(), []);

        await handler.HandleAsync(new HostWebSocketConnection(socket, request, "ws-a/webrtc-1"), CancellationToken.None);

        Assert.False(resolver.Client.CreatePeerCalled);
    }

    private static (WebRtcSignalingWebSocketHandler Handler, WebRtcSignalingSession Session) NewHandler(
        FakePeerConnection peer,
        TimeSpan? answerDeadline = null)
    {
        var client = new RecordingWebRtcClient(peer);
        var channel = new WebRtcVoiceChannel("webrtc-1", "Browser Voice", CommunicationPlugin.Id, client);
        var session = new WebRtcSignalingSession(
            client, channel, "call-webrtc-1", new CallTarget("webrtc:browser-x", "Bob"));
        var resolver = new FakeSessionResolver(session);
        var handler = new WebRtcSignalingWebSocketHandler(
            resolver,
            NullLogger<WebRtcSignalingWebSocketHandler>.Instance,
            answerDeadline);
        return (handler, session);
    }

    private static HostWebSocketConnection Connection(FakeSignalingWebSocket socket, WebRtcSignalingSession session)
    {
        var request = new HostWebSocketConnectRequest(
            CommunicationPlugin.Id, "webrtc/tok", new Dictionary<string, string>(),
            new Dictionary<string, string[]>(), []);
        return new HostWebSocketConnection(socket, request, "ws-a/webrtc-1");
    }

    private static string Frame(string type, string? sdp = null, string? candidate = null) =>
        WebRtcSignalMessage.TryParse(new WebRtcSignalMessage(type, sdp, candidate).ToJson())!.ToJson();
}

/// <summary>An <see cref="IWebRtcSignalingSessionResolver"/> that returns a fixed session (or none).</summary>
internal sealed class FakeSessionResolver(WebRtcSignalingSession? session) : IWebRtcSignalingSessionResolver
{
    public RecordingWebRtcClient Client { get; } = session?.Client as RecordingWebRtcClient ?? new RecordingWebRtcClient(new FakePeerConnection());

    public ValueTask<WebRtcSignalingSession?> ResolveAsync(string? subject, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(session);
}

/// <summary>An <see cref="IWebRtcClient"/> that hands out one pre-built peer and records the creation.</summary>
internal sealed class RecordingWebRtcClient(FakePeerConnection peer) : IWebRtcClient
{
    public bool CreatePeerCalled { get; private set; }

    public IPeerConnection CreatePeer()
    {
        CreatePeerCalled = true;
        return peer;
    }

    public IPeerConnectionManager Peers => throw new NotSupportedException();

    public IWebRtcModuleRegistry Modules => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
