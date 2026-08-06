using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Application.RealtimeMedia;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// The <see cref="IConferenceService"/> implementation: a Selective Forwarding Unit over the neutral media
/// provider port. It creates one server <see cref="IMediaPeer"/> per participant through
/// <see cref="IRealtimeMediaProvider"/>, wires the reciprocal send-only track topology and inbound fan-out
/// through the <see cref="ConferenceMediaRouter"/>, and hands the vertical a transport-neutral
/// <see cref="IConferenceParticipant"/> session carrying the initial offer and the renegotiation/candidate
/// events. No SDK or media type crosses the boundary — the vertical relays only SDP/candidates.
/// </summary>
internal sealed class ConferenceService : IConferenceService
{
    private readonly IRealtimeMediaProvider _provider;
    private readonly MediaPeerOptions _peerOptions;
    private readonly ConferenceMediaRouter _router;
    private readonly ILogger _logger;

    /// <summary>Creates the service over a media provider and the per-peer options the SFU peers are built with.</summary>
    public ConferenceService(IRealtimeMediaProvider provider, MediaPeerOptions peerOptions, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(peerOptions);

        _provider = provider;
        _peerOptions = peerOptions;
        _logger = logger ?? NullLogger.Instance;
        _router = new ConferenceMediaRouter(_logger);
    }

    /// <inheritdoc />
    public Task<IConferenceParticipant> JoinAsync(string conferenceId, string participantId, CancellationToken ct = default) =>
        JoinCoreAsync(conferenceId, participantId, policy: null, ct);

    /// <inheritdoc />
    public Task<IConferenceParticipant> JoinAsync(
        string conferenceId,
        string participantId,
        ConferencePolicy policy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        return JoinCoreAsync(conferenceId, participantId, policy, ct);
    }

    /// <inheritdoc />
    public ConferencePolicy GetPolicy(string conferenceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conferenceId);
        return _router.GetPolicy(conferenceId);
    }

    private async Task<IConferenceParticipant> JoinCoreAsync(
        string conferenceId,
        string participantId,
        ConferencePolicy? policy,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conferenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);

        // Settle the policy before any media is set up: a room that turns out to be under a different
        // obligation than the caller assumed must fail before it has a peer and tracks to unwind.
        _router.ApplyPolicy(conferenceId, policy);

        var peer = _provider.CreatePeer(_peerOptions);

        // The session's leave hook removes the participant from the SFU topology (and renegotiates the rest)
        // before it disposes its own peer; captures the ids so DisposeAsync routes to the right conference.
        var session = new ConferenceParticipant(
            peer,
            () =>
            {
                _router.ParticipantLeft(conferenceId, participantId);
                return ValueTask.CompletedTask;
            },
            _logger);

        // Wire the topology first (reciprocal tracks + fan-out + PLI bridge, renegotiating the existing
        // participants), then produce this participant's initial offer so it reflects the wired tracks.
        _router.ParticipantJoined(conferenceId, participantId, session, ct);
        await session.InitializeAsync(ct).ConfigureAwait(false);

        _logger.LogDebug(
            "ConferenceService: participant {ParticipantId} joined conference {ConferenceId}.",
            participantId, conferenceId);

        return session;
    }
}
