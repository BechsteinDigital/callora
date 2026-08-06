namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Channel-neutral handle for one call. Implementations are provided by
/// communication plugins; consumers never see protocol details.
/// </summary>
public interface ICall
{
    /// <summary>Stable call identifier unique within the owning channel.</summary>
    string CallId { get; }

    /// <summary>Current lifecycle state.</summary>
    CallState State { get; }

    /// <summary>Direction of the call.</summary>
    CallDirection Direction { get; }

    /// <summary>Remote participant of the call.</summary>
    CallTarget Target { get; }

    /// <summary>
    /// Why the call ended, set no later than the transition to <see cref="CallState.Terminated"/>.
    /// <see langword="null"/> while the call is not yet terminated, or when it ended without a
    /// reportable cause.
    /// </summary>
    CallTerminationReason? TerminationReason { get; }

    /// <summary>
    /// Raised on every state transition. Implementations must raise transitions
    /// in order and must not raise after <see cref="CallState.Terminated"/>.
    /// </summary>
    event EventHandler<CallStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Accepts an inbound ringing call; on success the call transitions to
    /// <see cref="CallState.Connected"/>. Throws <see cref="InvalidOperationException"/>
    /// when the call is not inbound or not in <see cref="CallState.Ringing"/>.
    /// </summary>
    Task AcceptAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an inbound ringing call; the channel translates the rejection
    /// into its protocol-specific cause and the call transitions to
    /// <see cref="CallState.Terminated"/>. Throws <see cref="InvalidOperationException"/>
    /// when the call is not inbound or not in <see cref="CallState.Ringing"/>.
    /// </summary>
    Task RejectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the call. Completes without error when the call is already terminated.
    /// </summary>
    Task HangupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one DTMF tone (0-9, *, #, A-D) to the remote party.
    /// </summary>
    Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised for every DTMF tone received from the remote party — the receive half of
    /// <see cref="SendDtmfAsync"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Not serialized.</b> Tones reach a call over two independent paths — out-of-band
    /// signalling and in-band media — which run on different threads. This event therefore carries
    /// no single-thread guarantee: handlers must be thread-safe, and two tones may be delivered
    /// concurrently. Keep handlers fast; they run on the path that raised them, so blocking one
    /// stalls signalling or media.</para>
    /// <para><b>Not de-duplicated.</b> A single keypress is routinely reported more than once; see
    /// <see cref="DtmfReceivedEventArgs"/> for why and what a consumer has to do about it.</para>
    /// </remarks>
    event EventHandler<DtmfReceivedEventArgs>? DtmfReceived;

    // Media access (audio/video/webrtc streams) is modality-specific and lives
    // on the communication plugin's own call type, not on this modality-neutral
    // contract — voice exposes audio, a future video plugin exposes video, etc.
    // (REV2 §10.1 C, ADR-012).
}
