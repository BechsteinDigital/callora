using Callora.Plugin.Communication.Abstractions.RealtimeMedia;

namespace Callora.Plugin.Communication.Abstractions.Conference;

/// <summary>
/// One participant's server-side conference session — a transport-neutral handle a vertical (a video
/// conference, a call centre) obtains from <see cref="IConferenceService.JoinAsync"/> and relays over its
/// own authenticated transport. The server is always the offerer: the session carries the initial
/// <see cref="InitialOffer"/> and re-offers via <see cref="OfferProduced"/> when the conference topology
/// changes (another participant joining or leaving). The vertical relays SDP/candidates in both directions
/// — <see cref="ApplyAnswerAsync"/> and <see cref="AddIceCandidateAsync"/> feed the browser's replies in,
/// while <see cref="OfferProduced"/> and <see cref="LocalIceCandidateProduced"/> carry the server's out.
/// No media type crosses this boundary: the session forwards encoded frames internally, the vertical sees
/// only <see cref="SessionDescription"/> and <see cref="IceCandidate"/>.
/// </summary>
/// <remarks>
/// <see cref="IAsyncDisposable.DisposeAsync"/> is the participant's <em>leave</em>: it tears down the
/// server peer and stops forwarding for this participant. The vertical disposes the session when its
/// signalling socket closes.
/// </remarks>
public interface IConferenceParticipant : IAsyncDisposable
{
    /// <summary>
    /// The first offer, produced by the server as offerer when the participant joined. The vertical relays
    /// it to the browser, whose answer comes back through <see cref="ApplyAnswerAsync"/>.
    /// </summary>
    SessionDescription InitialOffer { get; }

    /// <summary>
    /// Begins trickle ICE for this session — call it <em>after</em> subscribing to
    /// <see cref="LocalIceCandidateProduced"/> (and <see cref="OfferProduced"/>) and relaying the
    /// <see cref="InitialOffer"/>. It opens the trickle gate, flushes the candidates buffered while the offer
    /// was produced, then gathers server-reflexive (STUN) candidates — all surfaced through
    /// <see cref="LocalIceCandidateProduced"/>. Gathering is deferred out of
    /// <see cref="IConferenceService.JoinAsync"/> precisely so no candidate is raised before the vertical has
    /// subscribed (which would lose it); a browser only ever applies candidates for an offer it has seen.
    /// </summary>
    Task StartSignalingAsync(CancellationToken ct = default);

    /// <summary>
    /// Applies the browser's answer — the reply to <see cref="InitialOffer"/> or to a renegotiation offer
    /// raised by <see cref="OfferProduced"/>. Starting the transport happens once, on the first answer.
    /// </summary>
    Task ApplyAnswerAsync(SessionDescription answer, CancellationToken ct = default);

    /// <summary>Applies one remote ICE candidate trickled from the browser.</summary>
    Task AddIceCandidateAsync(IceCandidate candidate, CancellationToken ct = default);

    /// <summary>
    /// Raised with a fresh renegotiation offer when the conference topology changes for this participant
    /// (another participant joining or leaving). The vertical relays the offer to the browser and feeds the
    /// answer back through <see cref="ApplyAnswerAsync"/>.
    /// </summary>
    event EventHandler<SessionDescription>? OfferProduced;

    /// <summary>
    /// Raised with each locally gathered ICE candidate for the vertical to relay to the browser (trickle
    /// ICE). Candidates surfaced before <see cref="InitialOffer"/> is signalled are buffered and flushed
    /// once it is, so the browser never applies a candidate for an offer it has not seen.
    /// </summary>
    event EventHandler<IceCandidate>? LocalIceCandidateProduced;
}
