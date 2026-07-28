namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Protocol-neutral reason a call ended: a coarse <see cref="Category"/> plus optional protocol
/// detail (a SIP status code / reason phrase and a retry hint). Communication plugins translate
/// their transport-specific cause into this shape; consumers use <see cref="Category"/> for
/// decisions and the raw fields only for logging/diagnostics.
/// </summary>
/// <param name="Category">Coarse, protocol-neutral classification of the termination.</param>
/// <param name="SipStatusCode">The SIP status code, when the underlying protocol is SIP; otherwise <see langword="null"/>.</param>
/// <param name="ReasonPhrase">Human-readable protocol reason phrase (e.g. "Busy Here"), when reported.</param>
/// <param name="TerminatedBy">Which side ended the call.</param>
/// <param name="RetryAfterSeconds">A retry-after hint in seconds, when the protocol supplied one.</param>
public sealed record CallTerminationReason(
    CallTerminationCategory Category,
    int? SipStatusCode,
    string? ReasonPhrase,
    CallTerminatedBy TerminatedBy,
    int? RetryAfterSeconds);
