using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// The <see cref="IConferenceCallAttachment"/> implementation: resolves the call, checks that the room
/// will have it, and wires the two audio directions between the call and the conference topology.
/// </summary>
internal sealed class ConferenceCallAttachment : IConferenceCallAttachment
{
    private const int TelephonyPcmSampleRate = 8_000;
    private const int SamplesPer20MsFrame = 160;

    private readonly ConferenceService _conferences;
    private readonly ICallAccess _calls;
    private readonly IAudioTranscoderFactory _transcoders;
    private readonly ILogger _logger;
    private readonly Func<string, bool>? _isAnnouncing;

    /// <summary>Creates the attachment over the conference topology, the call registry and the codecs.</summary>
    public ConferenceCallAttachment(
        ConferenceService conferences,
        ICallAccess calls,
        IAudioTranscoderFactory transcoders,
        ILogger? logger = null,
        Func<string, bool>? isAnnouncing = null)
    {
        ArgumentNullException.ThrowIfNull(conferences);
        ArgumentNullException.ThrowIfNull(calls);
        ArgumentNullException.ThrowIfNull(transcoders);

        _conferences = conferences;
        _calls = calls;
        _transcoders = transcoders;
        _logger = logger ?? NullLogger.Instance;
        _isAnnouncing = isAnnouncing;
    }

    /// <inheritdoc />
    public async Task<IConferenceCallLeg> AttachAsync(
        string workspaceKey,
        string callId,
        string conferenceId,
        string participantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conferenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);

        var call = ResolveCall(workspaceKey, callId);
        EnsureTheRoomWillHaveACall(conferenceId);

        var audio = await call.OpenAudioAsync(cancellationToken).ConfigureAwait(false);

        var mixer = new ConferenceDownlinkMixer(
            _transcoders,
            ConferenceAudioCodec.Opus,
            ConferenceAudioCodec.G711Ulaw,
            TelephonyPcmSampleRate,
            SamplesPer20MsFrame);

        var endpoint = new CallConferenceEndpoint(
            mixer,
            _transcoders,
            ConferenceAudioCodec.G711Ulaw,
            ConferenceAudioCodec.Opus,
            TelephonyPcmSampleRate,
            SamplesPer20MsFrame,
            participantId);

        // Join the topology first, so the endpoint already renders every existing participant before
        // its own track surfaces — the same order the service uses for a browser participant.
        _conferences.Router.ParticipantJoined(conferenceId, participantId, endpoint, cancellationToken);
        endpoint.Start();

        return new ConferenceCallLeg(
            _conferences.Router,
            conferenceId,
            participantId,
            endpoint,
            mixer,
            audio,
            new PeriodicPacingClock(TimeSpan.FromMilliseconds(20)),
            _logger,
            _isAnnouncing is null ? null : () => _isAnnouncing(callId));
    }

    private IVoipCall ResolveCall(string workspaceKey, string callId)
    {
        if (_calls.Find(workspaceKey, callId) is not { } call)
        {
            throw new InvalidOperationException(
                $"Workspace '{workspaceKey}' has no active call '{callId}'.");
        }

        // A call without audio cannot be bridged. Reporting which of the two it is saves the reader
        // from guessing whether the call is missing or merely of the wrong kind.
        if (call is not IVoipCall voice)
        {
            throw new InvalidOperationException(
                $"Call '{callId}' carries no audio stream, so it cannot join a conference.");
        }

        return voice;
    }

    private void EnsureTheRoomWillHaveACall(string conferenceId)
    {
        if (_conferences.GetPolicy(conferenceId).RequiresEndToEndEncryption)
        {
            throw new InvalidOperationException(
                $"Conference '{conferenceId}' requires end-to-end encryption, which a bridged call cannot have: " +
                "the server must decrypt in order to transcode and mix for it. The room has to choose one or the " +
                "other, and it chose encryption.");
        }

        if (!_conferences.Router.IsHosted(conferenceId))
        {
            throw new InvalidOperationException(
                $"Conference '{conferenceId}' is not hosted on this node, so the call cannot be attached here. " +
                "A conference is process-bound: route the call to the node running the conference (SIP REFER after " +
                "resolving the room), or configure conference affinity.");
        }
    }
}
