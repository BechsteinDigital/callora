namespace Callora.Plugin.Communication.Application.Voice;

/// <summary>
/// A channel that can be told to stop taking new calls while the ones it already carries run out.
/// Implemented by channel types whose transport has a way to say "not here" — a SIP line can withdraw
/// its registration, a WebRTC channel can refuse to mint new sessions.
/// </summary>
/// <remarks>
/// This is the plugin's half of the host's drain contract (ADR-018 §2.1). It stays inside the plugin
/// rather than joining <see cref="Abstractions.ICommunicationChannel"/>, because how a channel refuses
/// work is protocol business and the neutral contract deliberately keeps protocol details out
/// (ADR-012, ADR-016).
/// </remarks>
internal interface IQuiescableChannel
{
    /// <summary>
    /// Number of calls currently up on this channel. Read during a drain to tell "still talking" from
    /// "done", and useful to an operator watching a deactivation.
    /// </summary>
    int ActiveCalls { get; }

    /// <summary>
    /// Stops accepting new calls. Calls already up are left alone — this refuses arrivals, it does not
    /// hang anything up.
    /// </summary>
    /// <remarks>Idempotent: quiescing an already-quiesced channel is a no-op.</remarks>
    ValueTask QuiesceAsync(CancellationToken cancellationToken = default);
}
