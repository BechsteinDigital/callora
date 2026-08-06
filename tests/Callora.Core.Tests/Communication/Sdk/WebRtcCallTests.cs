using System;
using System.Threading;
using System.Threading.Tasks;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using CalloraVoipSdk.WebRtc;
using Xunit;

namespace Callora.Core.Tests.Communication.Sdk;

/// <summary>
/// The WebRTC peer↔foundation call adapter (S2): maps the raw <see cref="FakePeerConnection"/> RFC 8829
/// lifecycle onto the four foundation states, derives a termination reason from the closing state, and
/// forwards hangup (peer close) and DTMF. Accept/reject are unsupported — a WebRTC call is established via
/// signalling, not a ringing/accept model.
/// </summary>
public sealed class WebRtcCallTests
{
    private static readonly CallTarget Peer = new("webrtc:browser-42");

    [Theory]
    [InlineData(PeerConnectionState.New, CallState.Connecting)]
    [InlineData(PeerConnectionState.Connecting, CallState.Connecting)]
    [InlineData(PeerConnectionState.Connected, CallState.Connected)]
    [InlineData(PeerConnectionState.Disconnected, CallState.Terminated)]
    [InlineData(PeerConnectionState.Failed, CallState.Terminated)]
    [InlineData(PeerConnectionState.Closed, CallState.Terminated)]
    public void State_MapsEveryPeerState(PeerConnectionState peerState, CallState expected)
    {
        var call = NewCall(new FakePeerConnection { State = peerState });

        Assert.Equal(expected, call.State);
    }

    [Fact]
    public void Properties_ProjectConstructorArguments()
    {
        var call = NewCall(new FakePeerConnection { State = PeerConnectionState.Connected }, "call-1", CallDirection.Inbound);

        Assert.Equal("call-1", call.CallId);
        Assert.Equal(CallDirection.Inbound, call.Direction);
        Assert.Equal("webrtc:browser-42", call.Target.Value);
        Assert.Equal(CallState.Connected, call.State);
    }

    [Fact]
    public void StateChanged_ReRaisesMappedTransition()
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.New };
        var call = NewCall(peer);
        CallStateChangedEventArgs? raised = null;
        call.StateChanged += (_, e) => raised = e;

        peer.RaiseStateChanged(PeerConnectionState.Connected);

        Assert.NotNull(raised);
        Assert.Equal(CallState.Connecting, raised!.PreviousState);
        Assert.Equal(CallState.Connected, raised.CurrentState);
    }

    [Fact]
    public void StateChanged_SuppressesCollapsedNoOp()
    {
        // New and Connecting both map to foundation Connecting: no visible transition.
        var peer = new FakePeerConnection { State = PeerConnectionState.New };
        var call = NewCall(peer);
        var count = 0;
        call.StateChanged += (_, _) => count++;

        peer.RaiseStateChanged(PeerConnectionState.Connecting);

        Assert.Equal(0, count);
        Assert.Equal(CallState.Connecting, call.State);
    }

    [Fact]
    public void StateChanged_FiresOnceForTerminatedAndDetaches()
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);
        var count = 0;
        call.StateChanged += (_, _) => count++;

        peer.RaiseStateChanged(PeerConnectionState.Closed);
        peer.RaiseStateChanged(PeerConnectionState.Closed); // no subscriber left → ignored

        Assert.Equal(1, count);
        Assert.False(peer.HasStateChangedSubscribers); // adapter unsubscribed — will not outlive the call
    }

    [Theory]
    [InlineData('5')]
    [InlineData('*')]
    [InlineData('#')]
    [InlineData('D')]
    public void DtmfReceived_DecodesEventCodeBackToItsSymbol(char symbol)
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);
        DtmfReceivedEventArgs? raised = null;
        call.DtmfReceived += (_, e) => raised = e;

        // The peer reports the RFC 4733 event code, not the character — the adapter has to decode it.
        peer.RaiseDtmfReceived(symbol, durationMs: 200);

        Assert.NotNull(raised);
        Assert.Equal(symbol, raised!.Tone);
        Assert.Equal(200, raised.DurationMs);
    }

    [Fact]
    public void DtmfReceived_DetachesFromPeerAfterTerminated()
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);
        var count = 0;
        call.DtmfReceived += (_, _) => count++;

        peer.RaiseStateChanged(PeerConnectionState.Closed);
        peer.RaiseDtmfReceived('1', durationMs: 100);

        Assert.Equal(0, count);
    }

    [Fact]
    public void TerminationReason_NullBeforeTerminated()
    {
        var call = NewCall(new FakePeerConnection { State = PeerConnectionState.Connected });

        Assert.Null(call.TerminationReason);
    }

    [Fact]
    public void TerminationReason_FailedMapsToFailedCategory()
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);

        peer.RaiseStateChanged(PeerConnectionState.Failed);

        var reason = call.TerminationReason;
        Assert.NotNull(reason);
        Assert.Equal(CallTerminationCategory.Failed, reason!.Category);
        Assert.Null(reason.SipStatusCode); // WebRTC has no SIP status
        Assert.Null(reason.ReasonPhrase);
    }

    [Theory]
    [InlineData(PeerConnectionState.Closed)]
    [InlineData(PeerConnectionState.Disconnected)]
    public void TerminationReason_NormalTeardownMapsToCompleted(PeerConnectionState terminal)
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);

        peer.RaiseStateChanged(terminal);

        var reason = call.TerminationReason;
        Assert.NotNull(reason);
        Assert.Equal(CallTerminationCategory.Completed, reason!.Category);
        Assert.Null(reason.SipStatusCode);
    }

    [Fact]
    public void TerminationReason_ReadableInStateChangedHandler()
    {
        // The reason must be set no later than the Terminated transition.
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);
        CallTerminationReason? observed = null;
        call.StateChanged += (_, e) =>
        {
            if (e.CurrentState == CallState.Terminated)
            {
                observed = call.TerminationReason;
            }
        };

        peer.RaiseStateChanged(PeerConnectionState.Failed);

        Assert.NotNull(observed);
        Assert.Equal(CallTerminationCategory.Failed, observed!.Category);
    }

    [Fact]
    public async Task HangupAsync_ClosesThePeer()
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);

        await call.HangupAsync();

        Assert.Equal(1, peer.DisposeCount);
    }

    [Fact]
    public async Task HangupAsync_IsIdempotentAfterTerminated()
    {
        var peer = new FakePeerConnection { State = PeerConnectionState.Connected };
        var call = NewCall(peer);
        peer.RaiseStateChanged(PeerConnectionState.Closed);

        await call.HangupAsync(); // already terminated → no-op

        Assert.Equal(0, peer.DisposeCount);
    }

    [Fact]
    public async Task SendDtmfAsync_ForwardsMappedToneCode()
    {
        var peer = new FakePeerConnection();
        var call = NewCall(peer);

        await call.SendDtmfAsync('5');
        await call.SendDtmfAsync('#');

        Assert.Equal([(byte)5, (byte)11], peer.SentDtmf); // RFC 4733: digit 5 → 5, '#' → 11
    }

    [Fact]
    public async Task SendDtmfAsync_InvalidTone_Throws()
    {
        var call = NewCall(new FakePeerConnection());

        await Assert.ThrowsAsync<ArgumentException>(() => call.SendDtmfAsync('Z'));
    }

    [Fact]
    public async Task AcceptAsync_Throws()
    {
        var call = NewCall(new FakePeerConnection());

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.AcceptAsync());
    }

    [Fact]
    public async Task RejectAsync_Throws()
    {
        var call = NewCall(new FakePeerConnection());

        await Assert.ThrowsAsync<InvalidOperationException>(() => call.RejectAsync());
    }

    private static WebRtcCall NewCall(
        FakePeerConnection peer,
        string callId = "call-1",
        CallDirection direction = CallDirection.Inbound) =>
        new(peer, callId, direction, Peer);
}
