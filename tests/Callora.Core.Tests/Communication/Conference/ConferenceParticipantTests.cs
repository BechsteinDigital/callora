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
    public async Task TrickleGate_BuffersCandidatesDuringOffer_ThenFlushes()
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
        var flushed = Assert.Single(relayed);          // flushed immediately after the offer
        Assert.Equal("early", flushed.Candidate);
        Assert.Equal("offer", session.InitialOffer.Type);
    }

    [Fact]
    public async Task Renegotiate_ProducesFreshDistinctOffer()
    {
        var peer = new FakeMediaPeer();
        var session = NewSession(peer);
        await session.InitializeAsync();

        SessionDescription? reoffer = null;
        session.OfferProduced += (_, o) => reoffer = o;

        await session.RenegotiateAsync();

        Assert.NotNull(reoffer);
        Assert.Equal("offer", reoffer!.Type);
        Assert.NotEqual(session.InitialOffer.Sdp, reoffer.Sdp); // a second, distinct offer
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
