using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// The inbound track of an endpoint that has no WebRTC transport: what the caller says, transcoded
/// into the conference's codec and raised exactly like a remote track's frames, so the forwarding path
/// fans it out to every other participant without knowing where it came from.
/// </summary>
internal sealed class ConferenceUplinkTrack : IRemoteMediaTrack
{
    /// <summary>Creates the track for one participant's audio.</summary>
    public ConferenceUplinkTrack(string participantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        StreamId = participantId;
    }

    /// <inheritdoc />
    public MediaTrackKind Kind => MediaTrackKind.Audio;

    /// <inheritdoc />
    public string? StreamId { get; }

    /// <inheritdoc />
    public event EventHandler<MediaFrame>? FrameReceived;

    /// <summary>Raises one transcoded frame to the forwarding path.</summary>
    public void Raise(MediaFrame frame) => FrameReceived?.Invoke(this, frame);
}
