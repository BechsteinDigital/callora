using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Abstractions.Conference;
using Callora.Plugin.Communication.Application.Streaming.Pacing;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Conference;

/// <summary>
/// One call's membership in a conference: it carries the call's audio into the room, drives the mixed
/// audio back out at the frame cadence, and unwinds all of that on dispose — without ending the call.
/// </summary>
internal sealed class ConferenceCallLeg : IConferenceCallLeg
{
    private readonly ConferenceMediaRouter _router;
    private readonly string _conferenceId;
    private readonly string _participantId;
    private readonly CallConferenceEndpoint _endpoint;
    private readonly ConferenceDownlinkMixer _mixer;
    private readonly ICallAudioStream _audio;
    private readonly PeriodicPacingClock _clock;
    private readonly CancellationTokenSource _stopping = new();
    private readonly EventHandler<AudioFrameReceivedEventArgs> _onInboundFrame;
    private readonly Task _pumping;
    private bool _disposed;

    /// <summary>Wires the call's audio to the endpoint and starts the downlink.</summary>
    public ConferenceCallLeg(
        ConferenceMediaRouter router,
        string conferenceId,
        string participantId,
        CallConferenceEndpoint endpoint,
        ConferenceDownlinkMixer mixer,
        ICallAudioStream audio,
        PeriodicPacingClock clock,
        ILogger logger)
    {
        _router = router;
        _conferenceId = conferenceId;
        _participantId = participantId;
        _endpoint = endpoint;
        _mixer = mixer;
        _audio = audio;
        _clock = clock;

        _onInboundFrame = (_, e) => _endpoint.PushFromCall(e.Frame.Span);
        _audio.FrameReceived += _onInboundFrame;

        var pump = new ConferenceDownlinkPump(mixer, _audio.SendAsync, clock, logger);
        _pumping = pump.RunAsync(_stopping.Token);
    }

    /// <inheritdoc />
    public bool IsMuted => _endpoint.IsMuted;

    /// <inheritdoc />
    public Task SetMutedAsync(bool muted, CancellationToken cancellationToken = default)
    {
        _endpoint.IsMuted = muted;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Takes the call out of the conference and leaves it running. The audio stream is not disposed
    /// either: it belongs to the call, which outlives this membership and may well join another room.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop feeding the room before leaving it, so no frame arrives from a participant the topology
        // has already dropped.
        _audio.FrameReceived -= _onInboundFrame;
        _router.ParticipantLeft(_conferenceId, _participantId);

        await _stopping.CancelAsync().ConfigureAwait(false);
        await _pumping.ConfigureAwait(false);

        _stopping.Dispose();
        _clock.Dispose();
        _endpoint.Dispose();
        _mixer.Dispose();
    }
}
