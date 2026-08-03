using System.Collections.Generic;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions.RealtimeMedia;
using Callora.Plugin.Communication.Application.Conference;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Conference;

/// <summary>
/// The participant negotiation session in isolation (ported hardenings of the VC
/// <c>RoomSignalingNegotiation</c>): the trickle-gate buffers local candidates until the initial offer is
/// produced then flushes them, renegotiation raises a fresh distinct offer, and a renegotiation before the
/// initial offer is a no-op.
/// </summary>
public sealed class ConferenceParticipantTests
{
    private static ConferenceParticipant NewSession(FakeMediaPeer peer) =>
        new(peer, () => ValueTask.CompletedTask, NullLogger.Instance);

    [Fact]
    public async Task TrickleGate_BuffersCandidatesUntilStartSignaling_ThenFlushes()
    {
        var peer = new FakeMediaPeer();
        var session = NewSession(peer);
        var relayed = new List<IceCandidate>();
        session.LocalIceCandidateProduced += (_, c) => relayed.Add(c);

        // A candidate surfaced during CreateOffer (after the session subscribed, before the offer is
        // signalled) must be buffered, not relayed straight through.
        var seenDuringOffer = -1;
        peer.OnCreateOffer = () =>
        {
            peer.RaiseLocalIceCandidate(new IceCandidate("early"));
            seenDuringOffer = relayed.Count; // still gated → 0
        };

        await session.InitializeAsync();

        Assert.Equal(0, seenDuringOffer);              // buffered while the offer was being produced
        Assert.Empty(relayed);                         // and NOT flushed by Initialize/JoinAsync — the vertical
                                                       // has not yet subscribed and started signalling
        Assert.Equal("offer", session.InitialOffer.Type);

        // The vertical relays the offer, then starts signalling → the buffered candidate flushes now.
        await session.StartSignalingAsync();

        var flushed = Assert.Single(relayed);
        Assert.Equal("early", flushed.Candidate);
    }

    [Fact]
    public async Task Renegotiate_ProducesFreshDistinctOffer()
    {
        var peer = new FakeMediaPeer();
        var session = NewSession(peer);
        await session.InitializeAsync();
        await session.StartSignalingAsync(); // the vertical starts signalling → renegotiation is enabled
        await session.ApplyAnswerAsync(new SessionDescription("answer", "sdp-1")); // the initial cycle completes first

        SessionDescription? reoffer = null;
        session.OfferProduced += (_, o) => reoffer = o;

        await session.RenegotiateAsync();

        Assert.NotNull(reoffer);
        Assert.Equal("offer", reoffer!.Type);
        Assert.NotEqual(session.InitialOffer.Sdp, reoffer.Sdp); // a second, distinct offer
        Assert.Equal(2, peer.CreateOfferCount);
    }

    [Fact]
    public async Task Renegotiate_WhileInitialOfferUnanswered_DefersUntilAnswerApplied()
    {
        var peer = new FakeMediaPeer();
        var session = NewSession(peer);
        await session.InitializeAsync();
        await session.StartSignalingAsync();

        var offers = new List<SessionDescription>();
        session.OfferProduced += (_, o) => offers.Add(o);

        // A topology change asks to renegotiate while the initial offer is still awaiting its answer. The
        // server must NOT supersede the in-flight offer (that would strand the browser's answer and leave the
        // peer's media session unbuilt) — the re-offer is deferred.
        await session.RenegotiateAsync();

        Assert.Empty(offers);
        Assert.Equal(1, peer.CreateOfferCount); // only the initial offer so far

        // The browser's answer to the initial offer lands → the deferred renegotiation is served now.
        await session.ApplyAnswerAsync(new SessionDescription("answer", "sdp-1"));

        var served = Assert.Single(offers);
        Assert.Equal("offer", served.Type);
        Assert.Equal(2, peer.CreateOfferCount); // initial + the deferred renegotiation
    }

    [Fact]
    public async Task Renegotiate_ManyWhileUnanswered_CollapseToOneOfferOnAnswer()
    {
        var peer = new FakeMediaPeer();
        var session = NewSession(peer);
        await session.InitializeAsync();
        await session.StartSignalingAsync();

        var offers = new List<SessionDescription>();
        session.OfferProduced += (_, o) => offers.Add(o);

        // Three participants join in quick succession before the initial answer — all deferred.
        await session.RenegotiateAsync();
        await session.RenegotiateAsync();
        await session.RenegotiateAsync();
        Assert.Empty(offers);

        await session.ApplyAnswerAsync(new SessionDescription("answer", "sdp-1"));

        // A single re-offer reflects the latest topology — pending renegotiations collapse into one.
        Assert.Single(offers);
        Assert.Equal(2, peer.CreateOfferCount);
    }

    [Fact]
    public async Task Renegotiate_BeforeInitialOffer_IsNoOp()
    {
        var peer = new FakeMediaPeer();
        var session = NewSession(peer);

        var fired = false;
        session.OfferProduced += (_, _) => fired = true;

        await session.RenegotiateAsync(); // No initial offer yet.

        Assert.False(fired);
        Assert.Equal(0, peer.CreateOfferCount);
    }

    [Fact]
    public async Task Dispose_IsIdempotent_AndDisposesPeerOnce()
    {
        var peer = new FakeMediaPeer();
        var leaveCount = 0;
        var session = new ConferenceParticipant(
            peer,
            () => { leaveCount++; return ValueTask.CompletedTask; },
            NullLogger.Instance);
        await session.InitializeAsync();

        await session.DisposeAsync();
        await session.DisposeAsync();

        Assert.Equal(1, peer.DisposeCount);
        Assert.Equal(1, leaveCount);
    }
}
