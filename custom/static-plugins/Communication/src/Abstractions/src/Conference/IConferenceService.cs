namespace Callora.Plugin.Communication.Abstractions.Conference;

/// <summary>
/// The transport-neutral conference contract communication exports for its vertical consumers (a video
/// conference, a call centre). A vertical never binds a media SDK: it joins a participant, relays the
/// resulting <see cref="IConferenceParticipant.InitialOffer"/> to the browser over its own authenticated
/// transport, feeds the answer and candidates back, and disposes the session on leave. Behind the contract
/// communication runs a Selective Forwarding Unit over the neutral media provider port — one server peer
/// per participant, encoded frames forwarded between them without transcoding.
/// </summary>
public interface IConferenceService
{
    /// <summary>
    /// Joins <paramref name="participantId"/> into the conference <paramref name="conferenceId"/>: the
    /// service creates a server peer, adds send-only outbound tracks for every other participant, subscribes
    /// to inbound tracks for fan-out, produces the initial offer, gathers candidates, and renegotiates the
    /// participants that gained a track for the joiner. Returns the participant's session — the vertical
    /// relays its offer/candidates and disposes it to leave.
    /// </summary>
    /// <param name="conferenceId">The conference the participant joins.</param>
    /// <param name="participantId">A stable identifier for the participant within this conference session.</param>
    /// <param name="ct">Cancellation for the join operation.</param>
    Task<IConferenceParticipant> JoinAsync(string conferenceId, string participantId, CancellationToken ct = default);

    /// <summary>
    /// Joins a participant and states what the conference requires of its members. The first stated
    /// policy takes effect for the room; a later join may restate the same policy or omit it, but
    /// stating a different one fails rather than silently picking a winner.
    /// </summary>
    /// <param name="conferenceId">The conference the participant joins.</param>
    /// <param name="participantId">A stable identifier for the participant within this conference session.</param>
    /// <param name="policy">What the conference requires of anything taking part in it.</param>
    /// <param name="ct">Cancellation for the join operation.</param>
    /// <exception cref="InvalidOperationException">A different policy is already in force for this conference.</exception>
    Task<IConferenceParticipant> JoinAsync(
        string conferenceId,
        string participantId,
        ConferencePolicy policy,
        CancellationToken ct = default);

    /// <summary>
    /// The policy in force for <paramref name="conferenceId"/>, or
    /// <see cref="ConferencePolicy.Unrestricted"/> when none was stated or the conference is unknown.
    /// </summary>
    ConferencePolicy GetPolicy(string conferenceId);
}
