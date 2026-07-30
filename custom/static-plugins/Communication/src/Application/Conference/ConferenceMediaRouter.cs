using System.Collections.Concurrent;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Selective Forwarding Unit over the neutral media provider port: forwards encoded frames between the
/// participants of a conference without decoding, mixing, or transcoding (the browsers do all codec work).
/// One <see cref="IMediaPeer"/> per participant. For every ordered pair of distinct participants (P, source)
/// the router adds a send-only video+audio track on P's peer with <c>StreamId = source</c>; an inbound
/// frame from a participant is copied and fanned out over each other peer's outbound track for that source,
/// carrying the source RTP timestamp 1:1 to preserve A/V sync.
/// <para>
/// Ported from the video conference's <c>SfuRoomMediaRouter</c> (<c>IPeerConnection</c> → <see cref="IMediaPeer"/>,
/// <c>EncodedFrame</c> → <see cref="MediaFrame"/>). Topology mutations (join/leave) run under a
/// per-conference lock; the forwarding path is lock-free over a snapshot. Renegotiation and key-frame
/// requests are fired outside the lock (fire-and-forget with error logging) because they perform network
/// sends. Unlike the VC router the peer is owned by the participant session, not disposed here — leave
/// unhooks handlers and drops the participant; the session disposes its own peer.
/// </para>
/// </summary>
internal sealed class ConferenceMediaRouter
{
    private readonly ConcurrentDictionary<string, Conference> _conferences = new(StringComparer.Ordinal);
    private readonly ILogger _logger;

    /// <summary>Initializes the router with an optional logger (defaults to a no-op).</summary>
    public ConferenceMediaRouter(ILogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Wires a joined participant into the conference SFU topology: adds the reciprocal send-only track
    /// pairs between the joiner and every existing participant, subscribes the joiner's inbound tracks for
    /// fan-out and its downstream PLI for upstream key-frame requests, then registers it. Runs the topology
    /// mutation under the conference lock; renegotiation of the affected participants and the joiner's
    /// initial key-frame priming are fired outside the lock (network sends). The joiner's own
    /// <see cref="ConferenceParticipant.InitializeAsync"/> (which produces the initial offer; candidate
    /// gather is deferred to <see cref="ConferenceParticipant.StartSignalingAsync"/> the vertical calls) is
    /// driven by the service <em>after</em> this returns, so the offer reflects the wired topology.
    /// </summary>
    public void ParticipantJoined(string conferenceId, string participantId, ConferenceParticipant session, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conferenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        ArgumentNullException.ThrowIfNull(session);

        var peer = session.Peer;
        var conference = _conferences.GetOrAdd(conferenceId, _ => new Conference());

        // Participants that gained a new outbound track for the joiner and therefore need a re-offer.
        // Collected under the lock, renegotiated/key-framed afterwards (network sends, no lock held).
        List<ConferenceParticipantEntry> affectedExisting;
        var joiner = new ConferenceParticipantEntry(participantId, peer, session);

        lock (conference.Gate)
        {
            affectedExisting = [.. conference.Participants.Values];

            foreach (var existing in affectedExisting)
            {
                // Existing peer renders the joiner's media…
                existing.Outbound[participantId] = AddOutboundTracks(existing.Peer, participantId);
                // …and the joiner's peer renders the existing participant's media.
                joiner.Outbound[existing.ParticipantId] = AddOutboundTracks(peer, existing.ParticipantId);
            }

            joiner.TrackReceivedHandler = (_, remoteTrack) =>
                remoteTrack.FrameReceived += (_, frame) =>
                    ForwardFrame(conferenceId, participantId, remoteTrack.Kind, frame);
            peer.RemoteTrackReceived += joiner.TrackReceivedHandler;

            // On each remote track, forward its frames. The inner FrameReceived handlers are not detached
            // individually on leave: the peer owns its remote tracks and is disposed on leave, and
            // ForwardFrame drops any frame from a source no longer in the conference — so a late frame is a
            // no-op. Tracking per-track handlers would mutate shared state from the receive-loop thread for
            // no functional gain, so it is deliberately avoided.

            // Downstream PLI on the joiner's peer carries no MID, so request a key frame from every current
            // upstream of the joiner (coarse but correct — the SDK does not attribute the PLI to a track).
            joiner.KeyFrameRequestedHandler = (_, _) =>
                RequestKeyFramesFromUpstreams(conferenceId, participantId);
            peer.KeyFrameRequested += joiner.KeyFrameRequestedHandler;

            conference.Participants[participantId] = joiner;
        }

        // Outside the lock: apply the new track topology on every affected existing socket and prime them
        // to send the joiner an intra frame. The joiner's own offer is produced by the service afterwards.
        foreach (var existing in affectedExisting)
        {
            FireRenegotiation(existing, ct);
            FireKeyFrameRequest(existing, participantId, ct);
        }

        _logger.LogDebug(
            "ConferenceMediaRouter: participant {ParticipantId} joined conference {ConferenceId} ({ExistingCount} existing peers wired).",
            participantId, conferenceId, affectedExisting.Count);
    }

    /// <summary>
    /// Removes a participant from the conference SFU topology: unhooks its receive/PLI handlers so no
    /// further frames are forwarded from it, drops it from the participant map, and renegotiates the
    /// remaining participants (whose outbound track for the leaver goes inert — no track-removal
    /// renegotiation, matching the VC behaviour). The peer itself is disposed by the participant session,
    /// not here. A now-empty conference is intentionally left in place (see the TOCTOU note below).
    /// </summary>
    public void ParticipantLeft(string conferenceId, string participantId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conferenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);

        if (!_conferences.TryGetValue(conferenceId, out var conference))
        {
            return;
        }

        ConferenceParticipantEntry? leaver;
        List<ConferenceParticipantEntry> remaining;

        lock (conference.Gate)
        {
            if (!conference.Participants.Remove(participantId, out leaver))
            {
                return;
            }

            // Unhook receive/PLI handlers so no further frames are forwarded from this peer. The leaver is
            // out of the snapshot now, so forwarding to it and other→leaver stops as well; the outbound
            // tracks other peers hold for the leaver go inert (tile removed via the vertical's roster).
            if (leaver.TrackReceivedHandler is not null)
            {
                leaver.Peer.RemoteTrackReceived -= leaver.TrackReceivedHandler;
            }

            if (leaver.KeyFrameRequestedHandler is not null)
            {
                leaver.Peer.KeyFrameRequested -= leaver.KeyFrameRequestedHandler;
            }

            remaining = [.. conference.Participants.Values];
        }

        // A now-empty Conference is intentionally left in _conferences. Removing it here is not race-safe: a
        // concurrent ParticipantJoined reads the same Conference via GetOrAdd *before* taking the lock, so
        // any check-empty-then-remove could evict a conference a joiner has just repopulated and wired,
        // orphaning that participant into a media black hole. An empty Conference holds no media resources;
        // entries are bounded by the count of distinct conference ids over the process lifetime (a
        // conference-closed teardown signal that could prune them is a separate follow-up).

        // Outside the lock: renegotiate the remaining participants so their session drops the leaver's now
        // inert track on the next offer (no track-removal renegotiation is issued for the leaver itself).
        foreach (var participant in remaining)
        {
            FireRenegotiation(participant, ct);
        }

        _logger.LogDebug(
            "ConferenceMediaRouter: participant {ParticipantId} left conference {ConferenceId} ({RemainingCount} remaining).",
            participantId, conferenceId, remaining.Count);
    }

    /// <summary>
    /// Forwards one inbound frame from <paramref name="sourceParticipantId"/> to every other participant's
    /// outbound track for that source. Runs synchronously on the media receive callback, so it copies the
    /// payload up front (the buffer is valid only during the callback) and never awaits — each send is
    /// fire-and-forget with per-consumer error isolation. Exposed internally so tests can drive the
    /// forwarding path directly.
    /// </summary>
    internal void ForwardFrame(string conferenceId, string sourceParticipantId, MediaTrackKind kind, MediaFrame frame)
    {
        if (!_conferences.TryGetValue(conferenceId, out var conference))
        {
            return;
        }

        // Copy-on-receive: the payload is only valid during the callback; the async fan-out below outlives
        // it. Mandatory even when a synchronous fake makes it look unnecessary.
        var payload = frame.Payload.ToArray();
        var timestamp = frame.RtpTimestamp;

        var participants = conference.Snapshot();

        // Drop a late frame from a source that has already left: other peers still hold an inert outbound
        // track for it (removed only via renegotiation), so forwarding onto it would resurrect a departed
        // participant's stream. The source must still be in the conference.
        if (!Array.Exists(participants, p => string.Equals(p.ParticipantId, sourceParticipantId, StringComparison.Ordinal)))
        {
            return;
        }

        foreach (var consumer in participants)
        {
            if (string.Equals(consumer.ParticipantId, sourceParticipantId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!consumer.Outbound.TryGetValue(sourceParticipantId, out var tracks))
            {
                continue;
            }

            SendFrame(consumer, kind, tracks, payload, timestamp, frame.IsKeyFrame, sourceParticipantId);
        }
    }

    private void SendFrame(
        ConferenceParticipantEntry consumer,
        MediaTrackKind kind,
        ConferenceOutboundTracks tracks,
        byte[] payload,
        uint? timestamp,
        bool isKeyFrame,
        string sourceParticipantId)
    {
        try
        {
            var track = kind == MediaTrackKind.Video ? tracks.Video : tracks.Audio;
            var outboundFrame = new MediaFrame(payload, timestamp, isKeyFrame, sourceParticipantId);
            var send = track.SendFrameAsync(outboundFrame, CancellationToken.None);

            // Fire-and-forget: the receive loop must not block. Observe faults so a dead/disposed consumer
            // peer never faults the whole fan-out.
            _ = ObserveSendAsync(send, consumer.ParticipantId, kind);
        }
        catch (Exception ex)
        {
            // Synchronous throw (e.g. a disposed peer): isolate it, keep forwarding to the rest.
            _logger.LogDebug(ex,
                "ConferenceMediaRouter: {Kind} frame send to {ParticipantId} threw synchronously — skipped.",
                kind, consumer.ParticipantId);
        }
    }

    private async Task ObserveSendAsync(Task send, string consumerParticipantId, MediaTrackKind kind)
    {
        try
        {
            await send.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "ConferenceMediaRouter: {Kind} frame send to {ParticipantId} failed — dropped.",
                kind, consumerParticipantId);
        }
    }

    private void RequestKeyFramesFromUpstreams(string conferenceId, string downstreamParticipantId)
    {
        if (!_conferences.TryGetValue(conferenceId, out var conference))
        {
            return;
        }

        foreach (var upstream in conference.Snapshot())
        {
            if (string.Equals(upstream.ParticipantId, downstreamParticipantId, StringComparison.Ordinal))
            {
                continue;
            }

            FireKeyFrameRequest(upstream, downstreamParticipantId, CancellationToken.None);
        }
    }

    private static ConferenceOutboundTracks AddOutboundTracks(IMediaPeer peer, string sourceParticipantId)
    {
        var video = peer.AddOutboundTrack(MediaTrackKind.Video, sourceParticipantId);
        var audio = peer.AddOutboundTrack(MediaTrackKind.Audio, sourceParticipantId);
        return new ConferenceOutboundTracks(video, audio);
    }

    private void FireRenegotiation(ConferenceParticipantEntry participant, CancellationToken ct)
    {
        _ = RenegotiateAsync(participant, ct);
    }

    private async Task RenegotiateAsync(ConferenceParticipantEntry participant, CancellationToken ct)
    {
        try
        {
            await participant.Session.RenegotiateAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ConferenceMediaRouter: renegotiation for participant {ParticipantId} failed.",
                participant.ParticipantId);
        }
    }

    private void FireKeyFrameRequest(ConferenceParticipantEntry upstream, string reason, CancellationToken ct)
    {
        _ = RequestKeyFrameAsync(upstream, reason, ct);
    }

    private async Task RequestKeyFrameAsync(ConferenceParticipantEntry upstream, string reason, CancellationToken ct)
    {
        try
        {
            await upstream.Peer.RequestKeyFrameAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex,
                "ConferenceMediaRouter: key-frame request to upstream {ParticipantId} (for {Reason}) failed.",
                upstream.ParticipantId, reason);
        }
    }
}
