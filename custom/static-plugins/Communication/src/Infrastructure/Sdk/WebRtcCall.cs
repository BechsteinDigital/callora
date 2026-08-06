using Callora.Plugin.Communication.Abstractions;
using CalloraVoipSdk.WebRtc;
using PeerDtmfTone = CalloraVoipSdk.WebRtc.DtmfTone;
using SdkDtmfTone = CalloraVoipSdk.Core.Domain.Calls.DtmfTone;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Adapts a raw CalloraVoipSdk WebRTC <see cref="IPeerConnection"/> to the foundation's
/// modality-neutral <see cref="ICall"/>, the WebRTC counterpart to the SIP <see cref="SdkCall"/>.
/// It maps the peer's RFC 8829 lifecycle onto the four foundation states, forwards hangup/DTMF to the
/// peer, and derives a <see cref="ICall.TerminationReason"/> from the closing state. The peer is a raw
/// transport primitive with no call model, so this adapter is what gives it call semantics.
/// </summary>
/// <remarks>
/// v1 is transport/control only: there is no server-side media access, so this is an <see cref="ICall"/>
/// and not an <see cref="IVoipCall"/> — media routing between peers (a conference SFU) and any
/// WebRTC↔SIP media bridge are Non-Goals of v1 and belong to their own consumers/slices.
/// A WebRTC call is established through the signalling path (S3), not through accept/reject, so those
/// actions are unsupported here.
/// </remarks>
internal sealed class WebRtcCall : ICall
{
    private readonly IPeerConnection _peer;
    private CallState _state;
    private CallTerminationReason? _terminationReason;
    private bool _detached;

    /// <summary>
    /// Wraps one live WebRTC peer as a call. <paramref name="callId"/> correlates the call across the
    /// signalling path, <paramref name="direction"/> records who initiated it, and
    /// <paramref name="target"/> is the remote participant (peer/user identity). Subscribes to the peer's
    /// lifecycle so foundation consumers see ordered state transitions.
    /// </summary>
    public WebRtcCall(IPeerConnection peer, string callId, CallDirection direction, CallTarget target)
    {
        ArgumentNullException.ThrowIfNull(peer);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentNullException.ThrowIfNull(target);

        _peer = peer;
        CallId = callId;
        Direction = direction;
        Target = target;
        _state = MapState(peer.State);
        _peer.ConnectionStateChanged += OnPeerStateChanged;
        _peer.DtmfReceived += OnPeerDtmfReceived;
    }

    /// <inheritdoc />
    public string CallId { get; }

    /// <inheritdoc />
    public CallState State => _state;

    /// <inheritdoc />
    public CallDirection Direction { get; }

    /// <inheritdoc />
    public CallTarget Target { get; }

    /// <inheritdoc />
    public CallTerminationReason? TerminationReason => _terminationReason;

    /// <inheritdoc />
    public event EventHandler<CallStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<DtmfReceivedEventArgs>? DtmfReceived;

    /// <summary>
    /// Not supported: a WebRTC call is established via signalling, not accept/reject — there is no
    /// ringing/accept model in v1.
    /// </summary>
    public Task AcceptAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "A WebRTC call is established via signalling, not accept/reject.");

    /// <summary>
    /// Not supported: a WebRTC call is established via signalling, not accept/reject — there is no
    /// ringing/reject model in v1.
    /// </summary>
    public Task RejectAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "A WebRTC call is established via signalling, not accept/reject.");

    /// <inheritdoc />
    public async Task HangupAsync(CancellationToken cancellationToken = default)
    {
        // The peer has no dedicated hangup; closing it (IAsyncDisposable) tears down ICE/DTLS/RTP and
        // drives the state to Closed. Idempotent: once terminated there is nothing to close.
        if (_state == CallState.Terminated)
        {
            return;
        }

        await _peer.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default)
    {
        // Mirror the SIP path's char→RFC 4733 mapping: the SDK's SIP DtmfTone validates the character and
        // exposes the event-code byte the WebRTC peer's SendDtmfAsync expects (throws on an invalid tone).
        var code = new SdkDtmfTone(tone).Code;
        return _peer.SendDtmfAsync(code, cancellationToken: cancellationToken);
    }

    // Lock-free like the SIP SdkCall path: this reads/writes _state (and _terminationReason/detach)
    // without a lock, which is safe only because ConnectionStateChanged is assumed to fire in order
    // and never concurrently for a single peer (RFC 8829: one peer's connection-state transitions are
    // serialized). Cross-thread reads of State/TerminationReason are unfenced (same as SdkCall) — revisit
    // if a signalling consumer reads them concurrently with a transition.
    private void OnPeerStateChanged(object? sender, PeerConnectionState peerState)
    {
        var current = MapState(peerState);
        if (current == _state)
        {
            // Several peer states collapse onto one foundation state (New/Connecting → Connecting,
            // Disconnected/Failed/Closed → Terminated); suppress the mapped no-ops.
            return;
        }

        var previous = _state;

        // Set the termination reason no later than the Terminated transition, so consumers reading it in
        // the StateChanged handler already see the cause.
        if (current == CallState.Terminated)
        {
            _terminationReason = MapTerminationReason(peerState);
        }

        _state = current;
        StateChanged?.Invoke(this, new CallStateChangedEventArgs(previous, current));

        // No peer events follow a terminal state — detach so the adapter does not outlive the call.
        if (current == CallState.Terminated)
        {
            DetachFromPeer();
        }
    }

    private void DetachFromPeer()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _peer.ConnectionStateChanged -= OnPeerStateChanged;
        _peer.DtmfReceived -= OnPeerDtmfReceived;
    }

    // The peer reports the RFC 4733 event code; the SIP DtmfTone owns the code↔character table, so
    // decoding through it keeps both paths on one mapping instead of a second copy here.
    private void OnPeerDtmfReceived(object? sender, PeerDtmfTone tone) =>
        DtmfReceived?.Invoke(
            this,
            new DtmfReceivedEventArgs(SdkDtmfTone.FromCode(tone.ToneCode).Symbol, tone.DurationMs));

    private static CallState MapState(PeerConnectionState state) => state switch
    {
        PeerConnectionState.New or PeerConnectionState.Connecting => CallState.Connecting,
        PeerConnectionState.Connected => CallState.Connected,
        PeerConnectionState.Disconnected or PeerConnectionState.Failed or PeerConnectionState.Closed =>
            CallState.Terminated,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown WebRTC peer state."),
    };

    private static CallTerminationReason MapTerminationReason(PeerConnectionState state)
    {
        // WebRTC has no SIP status, and the peer state alone does not reveal which side tore the call
        // down, so the terminating side stays Unknown. Only the coarse category is derivable: Failed for
        // an unrecoverable failure, Completed for a normal teardown (Closed / Disconnected).
        var category = state == PeerConnectionState.Failed
            ? CallTerminationCategory.Failed
            : CallTerminationCategory.Completed;

        return new CallTerminationReason(
            category,
            SipStatusCode: null,
            ReasonPhrase: null,
            CallTerminatedBy.Unknown,
            RetryAfterSeconds: null);
    }
}
