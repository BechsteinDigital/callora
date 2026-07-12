namespace Callora.Contracts.Communication;

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
}
