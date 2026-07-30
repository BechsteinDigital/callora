using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Abstractions.RealtimeMedia;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Owns one server <see cref="IMediaPeer"/>'s signalling lifecycle for a participant's conference session:
/// produces the initial offer, applies the browser's answer and remote candidates, trickles local
/// candidates (buffered until the offer is produced), and supports mid-call renegotiation
/// (<see cref="RenegotiateAsync"/>, RFC 8829) when the conference topology changes. The server is always
/// the offerer. Ported from the video conference's <c>RoomSignalingNegotiation</c>: instead of a WebSocket
/// channel it raises <see cref="OfferProduced"/>/<see cref="LocalIceCandidateProduced"/> for the vertical
/// to relay, and instead of a <c>requestRenegotiation</c> delegate the router calls
/// <see cref="RenegotiateAsync"/> directly (router and negotiation are the same plugin here).
/// <para>
/// The signalling-critical peer operations (CreateOffer, ApplyRemoteDescription, the one-time StartAsync)
/// are serialised through a <see cref="SemaphoreSlim"/> so a router-triggered renegotiation never races an
/// in-flight answer; the short trickle op <see cref="AddIceCandidateAsync"/> stays outside the gate.
/// StartAsync runs exactly once (the first answer); a renegotiation answer only re-applies the remote
/// description. A trickle-gate buffers local candidates until the initial offer is produced, then flushes
/// them, so the browser never applies a candidate for an offer it has not seen.
/// </para>
/// </summary>
internal sealed class ConferenceParticipant : IConferenceParticipant
{
    private readonly IMediaPeer _peer;
    private readonly Func<ValueTask> _onLeave;
    private readonly ILogger _logger;
    private readonly object _candidateGate = new();
    private readonly Queue<IceCandidate> _bufferedCandidates = new();

    // Serialises the peer's single-caller signalling operations: CreateOffer (initial offer +
    // renegotiation) and ApplyRemoteDescription/StartAsync in the answer path. A router-triggered
    // RenegotiateAsync runs concurrently with an answer the vertical is feeding in; without this gate two
    // CreateOffer/ApplyRemoteDescription calls would race. AddIceCandidateAsync is left OUTSIDE the gate
    // on purpose — a short, independent trickle op a long CreateOffer must never block.
    private readonly SemaphoreSlim _signalingGate = new(1, 1);

    private SessionDescription? _initialOffer;
    private bool _offerSent;

    // True once the transport has been started (first answer). A renegotiation answer only re-applies the
    // remote description — StartAsync must run exactly once (the transport is already live).
    private bool _started;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<SessionDescription>? OfferProduced;

    /// <inheritdoc />
    public event EventHandler<IceCandidate>? LocalIceCandidateProduced;

    /// <summary>
    /// Creates the session over a fresh server peer. The service calls <see cref="InitializeAsync"/> once
    /// the SFU topology (outbound tracks, inbound subscriptions) is wired, so the initial offer reflects it.
    /// </summary>
    /// <param name="peer">The owned server peer; disposed on leave.</param>
    /// <param name="onLeave">Invoked on <see cref="DisposeAsync"/> so the router stops forwarding and
    /// renegotiates the remaining participants before the peer is disposed.</param>
    /// <param name="logger">Diagnostics logger.</param>
    public ConferenceParticipant(IMediaPeer peer, Func<ValueTask> onLeave, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentNullException.ThrowIfNull(onLeave);
        ArgumentNullException.ThrowIfNull(logger);

        _peer = peer;
        _onLeave = onLeave;
        _logger = logger;
    }

    /// <inheritdoc />
    public SessionDescription InitialOffer =>
        _initialOffer ?? throw new InvalidOperationException("The conference session has not been initialised.");

    /// <summary>The owned server peer — the router forwards frames through it and requests key frames on it.</summary>
    public IMediaPeer Peer => _peer;

    /// <summary>
    /// Subscribes to the peer's ICE events and produces the initial offer (under the signalling gate) so
    /// <see cref="InitialOffer"/> is ready when <see cref="IConferenceService.JoinAsync"/> returns. It does
    /// NOT flush or gather candidates: the trickle gate stays closed and any candidate surfaced during
    /// CreateOffer is buffered. Gathering is deferred to <see cref="StartSignalingAsync"/> so nothing is
    /// raised before the vertical — which only obtains this session after JoinAsync returns — has subscribed.
    /// Called once by the service while wiring the SFU topology.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _peer.LocalIceCandidateDiscovered += OnLocalIceCandidateDiscovered;

        await _signalingGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _initialOffer = _peer.CreateOffer();
        }
        finally
        {
            _signalingGate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StartSignalingAsync(CancellationToken ct = default)
    {
        // Open the trickle gate and flush candidates buffered while the offer was produced — now with the
        // vertical subscribed, so they are relayed rather than lost.
        IceCandidate[] buffered;
        lock (_candidateGate)
        {
            _offerSent = true;
            buffered = [.. _bufferedCandidates];
            _bufferedCandidates.Clear();
        }

        foreach (var candidate in buffered)
        {
            LocalIceCandidateProduced?.Invoke(this, candidate);
        }

        // Gather STUN/server-reflexive candidates after the offer so they go through the open trickle gate.
        await _peer.GatherCandidatesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ApplyAnswerAsync(SessionDescription answer, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(answer);

        // ApplyRemoteDescription + the first StartAsync are serialised against a concurrent renegotiation
        // offer. StartAsync runs only on the FIRST answer; a renegotiation answer re-applies the remote
        // description but never restarts the already-live transport.
        await _signalingGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _peer.ApplyRemoteDescriptionAsync(answer, ct).ConfigureAwait(false);
            if (!_started)
            {
                _started = true;
                await _peer.StartAsync(ct).ConfigureAwait(false);
            }
        }
        finally
        {
            _signalingGate.Release();
        }
    }

    /// <inheritdoc />
    public Task AddIceCandidateAsync(IceCandidate candidate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        // Outside the signalling gate: a short, independent trickle op that must not be blocked by a long
        // CreateOffer/ApplyRemoteDescription.
        return _peer.AddIceCandidateAsync(candidate, ct);
    }

    /// <summary>
    /// Produces a FRESH offer and raises it via <see cref="OfferProduced"/>, driving a WebRTC renegotiation
    /// (RFC 8829): the server is always the offerer, so a topology change (another participant joining or
    /// leaving) is signalled by re-offering. Unlike <see cref="InitializeAsync"/> this does NOT gather
    /// candidates again — renegotiation reuses the established transport. Serialised through the signalling
    /// gate so it never races the initial offer or an in-flight answer. A no-op (debug log, never a throw)
    /// when the session is already disposed or the initial offer has not yet been produced. The
    /// disposed/offer-produced pre-check is a best-effort fast path outside the gate; a disposal that races
    /// past it surfaces as an <see cref="ObjectDisposedException"/> that is caught and swallowed, so the
    /// "no-op, never a throw" contract holds even under a concurrent teardown.
    /// </summary>
    public async Task RenegotiateAsync(CancellationToken ct = default)
    {
        if (_disposed || !_offerSent)
        {
            _logger.LogDebug(
                "Conference session: renegotiation skipped (disposed={Disposed}, offerProduced={OfferProduced}).",
                _disposed, _offerSent);
            return;
        }

        try
        {
            await _signalingGate.WaitAsync(ct).ConfigureAwait(false);
            SessionDescription offer;
            try
            {
                offer = _peer.CreateOffer();
            }
            finally
            {
                _signalingGate.Release();
            }

            OfferProduced?.Invoke(this, offer);
        }
        catch (ObjectDisposedException)
        {
            // The session was disposed concurrently — the signalling gate (or the peer) was torn down while
            // this renegotiation was entering. Renegotiation is a no-op after teardown by contract, so
            // swallow the race rather than surface a spurious throw to the (fire-and-forget) caller.
            _logger.LogDebug("Conference session: renegotiation raced disposal — skipped.");
        }
    }

    private void OnLocalIceCandidateDiscovered(object? sender, IceCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Candidate))
        {
            return;
        }

        lock (_candidateGate)
        {
            if (!_offerSent)
            {
                // Trickle-gate: hold candidates until the initial offer has been produced.
                _bufferedCandidates.Enqueue(candidate);
                return;
            }
        }

        LocalIceCandidateProduced?.Invoke(this, candidate);
    }

    /// <summary>
    /// Leaves the conference: detaches from the peer, tells the router to stop forwarding for this
    /// participant and renegotiate the rest, then disposes the peer. Idempotent.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _peer.LocalIceCandidateDiscovered -= OnLocalIceCandidateDiscovered;

        // Router cleanup (unhook receive/PLI handlers, drop from the participant map, renegotiate the rest)
        // before the peer is disposed so no frame is forwarded onto a torn-down peer.
        await _onLeave().ConfigureAwait(false);

        _signalingGate.Dispose();

        await _peer.DisposeAsync().ConfigureAwait(false);
    }
}
