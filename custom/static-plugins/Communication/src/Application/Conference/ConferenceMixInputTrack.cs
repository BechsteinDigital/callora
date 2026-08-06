using Callora.Plugin.Communication.Application.RealtimeMedia;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// An outbound track that ends in a mix instead of on a transport: the router sends one source's
/// frames onto it exactly as it would to a browser, and they become that source's contribution to the
/// single stream a non-mixing endpoint receives.
/// </summary>
internal sealed class ConferenceMixInputTrack : IMediaOutboundTrack
{
    private readonly ConferenceDownlinkMixer _mixer;
    private readonly string _sourceParticipantId;

    /// <summary>Creates the track that feeds <paramref name="sourceParticipantId"/> into the mix.</summary>
    public ConferenceMixInputTrack(ConferenceDownlinkMixer mixer, string sourceParticipantId)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceParticipantId);

        _mixer = mixer;
        _sourceParticipantId = sourceParticipantId;
    }

    /// <inheritdoc />
    public Task SendFrameAsync(MediaFrame frame, CancellationToken ct = default)
    {
        // Synchronous by nature: mixing is arithmetic, not a network send. The forwarding path calls
        // this from the media receive callback and does not await it, so it must not block either.
        _mixer.Push(_sourceParticipantId, frame.Payload.Span);
        return Task.CompletedTask;
    }
}
