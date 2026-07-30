using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Abstractions.RealtimeMedia;
using Callora.Plugin.Communication.Application.Conference;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// The conference SFU over the neutral media provider port (M2): join wires the reciprocal send-only track
/// topology and produces the server offer, inbound frames fan out to every other participant carrying the
/// source timestamp, leave stops forwarding without evicting the conference, a downstream PLI requests a key
/// frame from every upstream, and renegotiation re-offers on topology change. Driven entirely against
/// <see cref="FakeMediaPeer"/> — no SDK, no real media stack.
/// </summary>
public sealed class ConferenceServiceTests
{
    private const string Conf = "conf-1";

    private static (ConferenceService service, FakeRealtimeMediaProvider provider) NewService()
    {
        var provider = new FakeRealtimeMediaProvider();
        var service = new ConferenceService(provider, new MediaPeerOptions { EnableVideo = true });
        return (service, provider);
    }

    private static FakeMediaPeer PeerOf(IConferenceParticipant participant)
    {
        // The session exposes its owned peer to the router; reach it through the concrete type.
        var session = Assert.IsType<ConferenceParticipant>(participant);
        return Assert.IsType<FakeMediaPeer>(session.Peer);
    }

    [Fact]
    public async Task Join_ProducesInitialOffer_AndGathers()
    {
        var (service, provider) = NewService();

        var a = await service.JoinAsync(Conf, "A");

        Assert.Equal("offer", a.InitialOffer.Type);
        var peerA = provider.CreatedPeers[0];
        Assert.Equal(1, peerA.CreateOfferCount);
        Assert.Equal(1, peerA.GatherCount);
    }

    [Fact]
    public async Task Join_TwoWay_AddsReciprocalSendOnlyTracksKeyedBySource()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var b = await service.JoinAsync(Conf, "B");

        var peerA = PeerOf(a);
        var peerB = PeerOf(b);

        // A renders B's media (video + audio), keyed by StreamId=B; and B renders A's.
        Assert.NotNull(peerA.OutboundFor("B", MediaTrackKind.Video));
        Assert.NotNull(peerA.OutboundFor("B", MediaTrackKind.Audio));
        Assert.NotNull(peerB.OutboundFor("A", MediaTrackKind.Video));
        Assert.NotNull(peerB.OutboundFor("A", MediaTrackKind.Audio));

        // No self-tracks: A holds nothing for A, B nothing for B.
        Assert.Null(peerA.OutboundFor("A", MediaTrackKind.Video));
        Assert.Null(peerB.OutboundFor("B", MediaTrackKind.Video));
    }

    [Fact]
    public async Task Join_ThreeWay_EachPeerRendersEveryOtherSource()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var b = await service.JoinAsync(Conf, "B");
        var c = await service.JoinAsync(Conf, "C");

        var peerA = PeerOf(a);
        var peerB = PeerOf(b);
        var peerC = PeerOf(c);

        // Each peer holds a video+audio pair for each of the other two sources — six tracks per peer.
        Assert.Equal(4, peerA.OutboundTracks.Count(t => t.StreamId is "B" or "C"));
        Assert.Equal(4, peerB.OutboundTracks.Count(t => t.StreamId is "A" or "C"));
        Assert.Equal(4, peerC.OutboundTracks.Count(t => t.StreamId is "A" or "B"));
    }

    [Fact]
    public async Task Join_RenegotiatesAffectedExisting_ViaOfferProduced()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var reoffers = new List<SessionDescription>();
        a.OfferProduced += (_, offer) => reoffers.Add(offer);

        await service.JoinAsync(Conf, "B");

        // A gained an outbound track for B → the router renegotiated A: a second offer fired to A.
        var reoffer = Assert.Single(reoffers);
        Assert.Equal("offer", reoffer.Type);
        Assert.Equal(2, PeerOf(a).CreateOfferCount); // initial + renegotiation
    }

    [Fact]
    public async Task Join_PrimesInitialKeyFrameFromExistingUpstreams()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        await service.JoinAsync(Conf, "B");

        // On B's join, the router asks each existing upstream (A) for a key frame so B gets an intra frame.
        Assert.Equal(1, PeerOf(a).KeyFrameRequestCount);
    }

    [Fact]
    public async Task FrameFanOut_FromA_ReachesEveryOther_OnTrackForA_CarryingTimestamp()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var b = await service.JoinAsync(Conf, "B");
        var c = await service.JoinAsync(Conf, "C");

        // A publishes a video track; the router subscribes on RemoteTrackReceived.
        var trackA = new FakeRemoteMediaTrack(MediaTrackKind.Video, "A");
        PeerOf(a).RaiseRemoteTrackReceived(trackA);

        var payload = new byte[] { 1, 2, 3 };
        trackA.RaiseFrame(new MediaFrame(payload, RtpTimestamp: 4711u, IsKeyFrame: true, StreamId: "A"));

        // B and C each got the frame on their outbound-for-A video track; A got nothing (source excluded).
        var sentToB = Assert.Single(PeerOf(b).OutboundFor("A", MediaTrackKind.Video)!.SentFrames);
        var sentToC = Assert.Single(PeerOf(c).OutboundFor("A", MediaTrackKind.Video)!.SentFrames);
        Assert.Equal(4711u, sentToB.RtpTimestamp);
        Assert.Equal(4711u, sentToC.RtpTimestamp);
        Assert.Equal(payload, sentToB.Payload.ToArray());

        // The source's own tracks-for-others (A→B, A→C) never receive A's own frame.
        Assert.Empty(PeerOf(a).OutboundTracks.SelectMany(t => t.SentFrames));
    }

    [Fact]
    public async Task FrameFanOut_CopiesPayload_NotAliasingSourceBuffer()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var b = await service.JoinAsync(Conf, "B");

        var trackA = new FakeRemoteMediaTrack(MediaTrackKind.Video, "A");
        PeerOf(a).RaiseRemoteTrackReceived(trackA);

        var buffer = new byte[] { 7, 7, 7 };
        trackA.RaiseFrame(new MediaFrame(buffer, RtpTimestamp: 1u, IsKeyFrame: false, StreamId: "A"));

        // Mutate the source buffer after the callback: the forwarded frame must have been copied.
        buffer[0] = 99;

        var forwarded = Assert.Single(PeerOf(b).OutboundFor("A", MediaTrackKind.Video)!.SentFrames);
        Assert.Equal(new byte[] { 7, 7, 7 }, forwarded.Payload.ToArray());
    }

    [Fact]
    public async Task FrameFanOut_RoutesByKind()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var b = await service.JoinAsync(Conf, "B");

        var audioTrackA = new FakeRemoteMediaTrack(MediaTrackKind.Audio, "A");
        PeerOf(a).RaiseRemoteTrackReceived(audioTrackA);
        audioTrackA.RaiseFrame(new MediaFrame(new byte[] { 5 }, RtpTimestamp: null, IsKeyFrame: false, StreamId: "A"));

        // The audio frame lands on B's audio-for-A track, not the video one.
        Assert.Single(PeerOf(b).OutboundFor("A", MediaTrackKind.Audio)!.SentFrames);
        Assert.Empty(PeerOf(b).OutboundFor("A", MediaTrackKind.Video)!.SentFrames);
    }

    [Fact]
    public async Task Leave_DisposesPeer_AndStopsForwardingFromLeaver()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var b = await service.JoinAsync(Conf, "B");

        var trackA = new FakeRemoteMediaTrack(MediaTrackKind.Video, "A");
        var peerA = PeerOf(a);
        peerA.RaiseRemoteTrackReceived(trackA);

        await a.DisposeAsync(); // A leaves.

        Assert.Equal(1, peerA.DisposeCount);

        // A late frame from the departed source is dropped — B's outbound-for-A stays empty.
        trackA.RaiseFrame(new MediaFrame(new byte[] { 1 }, RtpTimestamp: 1u, IsKeyFrame: false, StreamId: "A"));
        Assert.Empty(PeerOf(b).OutboundFor("A", MediaTrackKind.Video)!.SentFrames);
    }

    [Fact]
    public async Task Leave_KeepsConferenceAlive_NextJoinReusesIt()
    {
        var (service, provider) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        await a.DisposeAsync(); // Conference is now empty.

        // A new join into the same (empty) conference succeeds and wires against the surviving conference.
        var b = await service.JoinAsync(Conf, "B");
        var c = await service.JoinAsync(Conf, "C");

        // B and C reciprocally render each other — the conference was not evicted and re-created wrongly.
        Assert.NotNull(PeerOf(b).OutboundFor("C", MediaTrackKind.Video));
        Assert.NotNull(PeerOf(c).OutboundFor("B", MediaTrackKind.Video));
        Assert.Equal(3, provider.CreatedPeers.Count); // A, B, C — one peer each.
    }

    [Fact]
    public async Task Pli_FromDownstream_RequestsKeyFrameFromEveryUpstream()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var b = await service.JoinAsync(Conf, "B");
        var c = await service.JoinAsync(Conf, "C");

        var peerA = PeerOf(a);
        var peerB = PeerOf(b);
        var peerC = PeerOf(c);

        // Baseline key-frame counts from join-time priming; measure the delta the PLI adds.
        var beforeA = peerA.KeyFrameRequestCount;
        var beforeB = peerB.KeyFrameRequestCount;
        var beforeC = peerC.KeyFrameRequestCount;

        // C's browser sends a PLI (no MID) → the router asks every upstream of C (A and B) for a key frame.
        peerC.RaiseKeyFrameRequested();

        Assert.Equal(beforeA + 1, peerA.KeyFrameRequestCount);
        Assert.Equal(beforeB + 1, peerB.KeyFrameRequestCount);
        Assert.Equal(beforeC, peerC.KeyFrameRequestCount); // C is the downstream — not asked itself.
    }

    [Fact]
    public async Task Renegotiate_SecondAnswer_DoesNotRestartTransport()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        var peerA = PeerOf(a);

        // First answer starts the transport.
        await a.ApplyAnswerAsync(new SessionDescription("answer", "sdp-1"));
        Assert.Equal(1, peerA.StartCount);

        // A renegotiation offer fires when B joins; A answers again — the transport is NOT restarted.
        await service.JoinAsync(Conf, "B");
        await a.ApplyAnswerAsync(new SessionDescription("answer", "sdp-2"));

        Assert.Equal(1, peerA.StartCount);
        Assert.Equal(2, peerA.AppliedAnswers.Count);
    }

    [Fact]
    public async Task LocalCandidates_TrickleAsLocalIceCandidateProduced()
    {
        var (service, _) = NewService();

        var candidates = new List<IceCandidate>();
        var a = await service.JoinAsync(Conf, "A");
        a.LocalIceCandidateProduced += (_, c) => candidates.Add(c);

        // After the initial offer the trickle gate is open — a candidate is relayed straight through.
        PeerOf(a).RaiseLocalIceCandidate(new IceCandidate("candidate:host 1 udp"));

        var relayed = Assert.Single(candidates);
        Assert.Equal("candidate:host 1 udp", relayed.Candidate);
    }

    [Fact]
    public async Task RemoteCandidate_IsAppliedToPeer()
    {
        var (service, _) = NewService();

        var a = await service.JoinAsync(Conf, "A");
        await a.AddIceCandidateAsync(new IceCandidate("candidate:remote 1 udp"));

        var applied = Assert.Single(PeerOf(a).AppliedCandidates);
        Assert.Equal("candidate:remote 1 udp", applied.Candidate);
    }
}
