using CalloraVoipSdk.WebRtc;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Api.WebSocket;

/// <summary>
/// Owns one server-side <see cref="IPeerConnection"/>'s signalling lifecycle for the lifetime of a
/// signalling socket: sends the offer and trickled local candidates, applies the browser's answer and
/// remote candidates, and attaches the peer to its channel as an incoming call once connected (exactly
/// once). Kept separate from the WebSocket read loop so the handler stays a thin transport shell and the
/// peer/event/cleanup bookkeeping lives in one focused place.
/// </summary>
internal sealed class WebRtcSignalingNegotiation(
    IPeerConnection peer,
    WebRtcSignalingSession session,
    WebRtcSignalingChannel channel,
    ILogger logger) : IAsyncDisposable
{
    private readonly object _candidateGate = new();
    private readonly Queue<string> _bufferedCandidates = new();
    private readonly CancellationTokenSource _lifetime = new();
    private bool _offerSent;

    // Claims peer ownership exactly once (0 = unclaimed). Both the connected-attach path and DisposeAsync
    // race for it via Interlocked.Exchange: the winner owns the peer. The attach winner wraps it in a live
    // WebRtcCall; the dispose winner disposes the unclaimed peer. This makes the two mutually exclusive, so
    // a socket-close/connect race can never dispose a peer a WebRtcCall is taking over (Interlock, not just
    // visibility).
    private int _peerClaimed;
    private bool _disposed;

    /// <summary>
    /// Subscribes to the peer's ICE/connection events, then produces and sends the local offer. The peer
    /// is the offerer, so the offer must reach the browser before any candidate for it: candidates the SDK
    /// surfaces at offer time (RFC 8838) are buffered and flushed right after the offer is sent; later ones
    /// trickle out immediately. The connected transition attaches the incoming call.
    /// </summary>
    public async ValueTask StartAsync(CancellationToken cancellationToken)
    {
        peer.LocalIceCandidateDiscovered += OnLocalIceCandidateDiscovered;
        peer.ConnectionStateChanged += OnConnectionStateChanged;

        var offer = peer.CreateOffer();
        await channel.SendAsync(WebRtcSignalMessage.Offer(offer), cancellationToken).ConfigureAwait(false);

        // Mark the offer sent and drain any candidate that arrived while creating it — the browser can now
        // apply them against the offer's session.
        string[] buffered;
        lock (_candidateGate)
        {
            _offerSent = true;
            buffered = [.. _bufferedCandidates];
            _bufferedCandidates.Clear();
        }

        foreach (var candidate in buffered)
        {
            await channel.SendAsync(WebRtcSignalMessage.IceCandidate(candidate), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Applies one inbound signalling frame to the peer.</summary>
    public async ValueTask HandleAsync(WebRtcSignalMessage message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case WebRtcSignalMessage.TypeAnswer when !string.IsNullOrWhiteSpace(message.Sdp):
                // Answer must be applied before the transport starts (RFC 8829: no ICE/DTLS without the
                // remote description). As the offerer, the returned SDP is our unchanged offer — ignored.
                await peer.SetRemoteDescriptionAsync(message.Sdp!, cancellationToken).ConfigureAwait(false);
                await peer.StartAsync(cancellationToken).ConfigureAwait(false);
                break;

            case WebRtcSignalMessage.TypeCandidate when !string.IsNullOrWhiteSpace(message.Candidate):
                await peer.AddIceCandidateAsync(message.Candidate!, cancellationToken).ConfigureAwait(false);
                break;

            default:
                // A well-formed frame we do not expect from the browser (e.g. a second offer, or a type
                // with its payload missing) — log and ignore rather than trust it.
                logger.LogWarning("WebRTC signalling: ignored an unexpected '{Type}' frame.", message.Type);
                break;
        }
    }

    private void OnLocalIceCandidateDiscovered(object? sender, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        lock (_candidateGate)
        {
            if (!_offerSent)
            {
                // Hold candidates surfaced before the offer was signalled — they are flushed in order once
                // it is, so the browser never sees a candidate for an offer it has not received.
                _bufferedCandidates.Enqueue(candidate);
                return;
            }
        }

        // Fire-and-forget: candidates trickle on SDK threads and the channel serializes sends. Bound the
        // send to this negotiation's lifetime so a late candidate after close does not send on a dead
        // socket; swallow the expected post-close faults, log the rest.
        _ = SendCandidateAsync(candidate);
    }

    private async Task SendCandidateAsync(string candidate)
    {
        try
        {
            // Read the token before awaiting: DisposeAsync may dispose the CTS concurrently, and reading a
            // disposed source's Token throws — capture it while it is still valid.
            var lifetimeToken = _lifetime.Token;
            await channel.SendAsync(WebRtcSignalMessage.IceCandidate(candidate), lifetimeToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            // Negotiation ended before the candidate could be flushed (cancelled, or the CTS was disposed
            // in a close race) — expected on close, not a fault.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WebRTC signalling: failed to send a local ICE candidate.");
        }
    }

    private void OnConnectionStateChanged(object? sender, PeerConnectionState state)
    {
        if (state != PeerConnectionState.Connected)
        {
            return;
        }

        // Claim the peer exactly once: the peer can re-enter Connected after a transient Disconnected (a
        // later claim loses and no-ops), and losing to a concurrent DisposeAsync means the socket already
        // closed — do not raise a call on a peer that is being torn down.
        if (Interlocked.Exchange(ref _peerClaimed, 1) != 0)
        {
            return;
        }

        // This negotiation won the claim: a WebRtcCall now owns the peer (it drives hangup/dispose), so
        // DisposeAsync will not touch it.
        session.Channel.TrackIncomingCall(peer, session.CallId, session.Target);
    }

    /// <summary>
    /// Detaches from the peer and, unless a call has taken it over, disposes it — so a socket that closes
    /// before the peer connected does not leak the peer.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        peer.LocalIceCandidateDiscovered -= OnLocalIceCandidateDiscovered;
        peer.ConnectionStateChanged -= OnConnectionStateChanged;

        await _lifetime.CancelAsync().ConfigureAwait(false);
        _lifetime.Dispose();

        // Race the same claim the connected-attach path uses: dispose the peer only if we win it. If the
        // connected handler already claimed it, a WebRtcCall owns the peer and must not be disposed here.
        if (Interlocked.Exchange(ref _peerClaimed, 1) == 0)
        {
            await peer.DisposeAsync().ConfigureAwait(false);
        }
    }
}
