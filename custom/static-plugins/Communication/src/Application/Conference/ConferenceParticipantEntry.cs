using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Per-participant SFU state inside a <see cref="Conference"/>: the owned server peer, the participant
/// session the router renegotiates directly (the router and the negotiation live in one plugin, so the VC
/// <c>requestRenegotiation</c> delegate seam is gone — the router calls
/// <see cref="ConferenceParticipant.RenegotiateAsync"/>), the outbound track pairs this peer renders every
/// other participant's media on, and the event subscriptions to unhook on leave. Mutated only under the
/// owning conference's lock; read lock-free by the frame-forwarding path via a snapshot of the participant
/// dictionary.
/// </summary>
internal sealed class ConferenceParticipantEntry
{
    /// <summary>Creates the entry for a joined participant.</summary>
    public ConferenceParticipantEntry(string participantId, IMediaPeer peer, ConferenceParticipant session)
    {
        ParticipantId = participantId;
        Peer = peer;
        Session = session;
    }

    /// <summary>Stable identifier for the participant in this conference session.</summary>
    public string ParticipantId { get; }

    /// <summary>The owned server peer; disposed by the session (leave) via its negotiation.</summary>
    public IMediaPeer Peer { get; }

    /// <summary>The participant's negotiation session; the router calls <see cref="ConferenceParticipant.RenegotiateAsync"/> on it after a topology change.</summary>
    public ConferenceParticipant Session { get; }

    /// <summary>sourceParticipantId → the track pair this peer renders that source's media on.</summary>
    public Dictionary<string, ConferenceOutboundTracks> Outbound { get; } = new(StringComparer.Ordinal);

    /// <summary>Handler bound to <see cref="IMediaPeer.RemoteTrackReceived"/>; unhooked on leave.</summary>
    public EventHandler<IRemoteMediaTrack>? TrackReceivedHandler { get; set; }

    /// <summary>Handler bound to <see cref="IMediaPeer.KeyFrameRequested"/>; unhooked on leave.</summary>
    public EventHandler? KeyFrameRequestedHandler { get; set; }
}
