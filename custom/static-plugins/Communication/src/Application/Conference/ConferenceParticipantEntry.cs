using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// Per-participant SFU state inside a <see cref="Conference"/>: the endpoint the router forwards
/// through and renegotiates after a topology change, the outbound track pairs it renders every other
/// participant's media on, and the event subscriptions to unhook on leave. Mutated only under the
/// owning conference's lock; read lock-free by the frame-forwarding path via a snapshot of the
/// participant dictionary.
/// </summary>
/// <remarks>
/// The entry holds an <see cref="IConferenceEndpoint"/> rather than a media peer and its negotiation
/// session. That is what lets a participant which cannot mix for itself — a telephone leg receives a
/// single stream — sit in the topology as an ordinary member: the forwarding path treats every entry
/// the same way, whatever backs it.
/// </remarks>
internal sealed class ConferenceParticipantEntry
{
    /// <summary>Creates the entry for a joined participant.</summary>
    public ConferenceParticipantEntry(string participantId, IConferenceEndpoint endpoint)
    {
        ParticipantId = participantId;
        Endpoint = endpoint;
    }

    /// <summary>Stable identifier for the participant in this conference session.</summary>
    public string ParticipantId { get; }

    /// <summary>Where this participant's frames arrive from and where its rendered tracks are added.</summary>
    public IConferenceEndpoint Endpoint { get; }

    /// <summary>sourceParticipantId → the track pair this participant renders that source's media on.</summary>
    public Dictionary<string, ConferenceOutboundTracks> Outbound { get; } = new(StringComparer.Ordinal);

    /// <summary>Handler bound to <see cref="IConferenceEndpoint.RemoteTrackReceived"/>; unhooked on leave.</summary>
    public EventHandler<IRemoteMediaTrack>? TrackReceivedHandler { get; set; }

    /// <summary>Handler bound to <see cref="IConferenceEndpoint.KeyFrameRequested"/>; unhooked on leave.</summary>
    public EventHandler? KeyFrameRequestedHandler { get; set; }
}
