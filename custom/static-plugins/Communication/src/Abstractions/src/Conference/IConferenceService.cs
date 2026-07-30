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
}
