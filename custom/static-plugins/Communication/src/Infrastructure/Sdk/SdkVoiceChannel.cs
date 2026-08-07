using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;
using CalloraVoipSdk.Core.Application.Media;
using CalloraVoipSdk.Core.Domain.Lines;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SdkIncomingCallEventArgs = CalloraVoipSdk.Core.Domain.Events.IncomingCallEventArgs;
using SdkLineStateChangedEventArgs = CalloraVoipSdk.Core.Domain.Events.LineStateChangedEventArgs;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Wraps one CalloraVoipSdk <see cref="IPhoneLine"/> as a foundation <see cref="IVoiceChannel"/>:
/// derives channel health from the line's registration state, surfaces inbound SDK calls as
/// foundation <see cref="ICall"/>s (via <see cref="SdkCall"/>), places outbound calls through
/// the line, and enforces a per-channel concurrent-call ceiling. Every call it produces is an
/// <see cref="IVoipCall"/> backed by the injected media tap factory, so consumers can open the
/// B4-deep-1 audio bridge on it.
/// </summary>
public sealed class SdkVoiceChannel : IVoiceChannel, IQuiescableChannel, IDisposable
{
    /// <summary>
    /// SIP status the channel answers with once it has been quiesced. Deliberately not the SDK's
    /// default 486 Busy Here: 503 tells the carrier this line is out of service right now, which sends
    /// the call down the next route in the trunk group instead of giving the caller a busy tone.
    /// </summary>
    private const int ServiceUnavailableStatus = 503;

    /// <summary>
    /// SIP status for an inbound call the account has no free line for. 486 rather than the drain's
    /// 503, because the two say different things to a carrier: out of service sends the call down
    /// another route and invites the carrier to stop routing here at all, which is the wrong answer to
    /// a busy minute. A full trunk is occupied, and it will free up.
    /// </summary>
    private const int BusyHereStatus = 486;

    private static readonly IReadOnlyCollection<string> VoiceCapability = [CommunicationCapabilities.Voice];

    private readonly IPhoneLine _line;
    private readonly Func<(IMediaReceiver Receiver, IMediaSender Sender)> _mediaTapFactory;
    private readonly int _maxConcurrentCalls;
    private readonly ILogger _logger;
    private int _activeCalls;
    private int _disposed;
    private int _quiesced;

    /// <summary>
    /// Wraps <paramref name="line"/> as a channel. <paramref name="mediaTapFactory"/> creates the
    /// per-call receiver/sender tap handed to each produced <see cref="SdkCall"/>.
    /// <paramref name="maxConcurrentCalls"/> is the maximum number of simultaneous calls allowed
    /// on this channel; outbound dials beyond this limit throw <see cref="InvalidOperationException"/>.
    /// </summary>
    public SdkVoiceChannel(
        string channelId,
        string displayName,
        string pluginId,
        IPhoneLine line,
        Func<(IMediaReceiver Receiver, IMediaSender Sender)> mediaTapFactory,
        int maxConcurrentCalls,
        ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(mediaTapFactory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxConcurrentCalls);

        ChannelId = channelId;
        DisplayName = displayName;
        PluginId = pluginId;
        _line = line;
        _mediaTapFactory = mediaTapFactory;
        _maxConcurrentCalls = maxConcurrentCalls;
        _logger = logger ?? NullLogger.Instance;
        _line.IncomingCall += OnSdkIncomingCall;
        _line.StateChanged += OnLineStateChanged;
    }

    /// <inheritdoc />
    public string ChannelId { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string PluginId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Capabilities => VoiceCapability;

    /// <inheritdoc />
    public ChannelHealth Health => MapHealth(_line.State);

    /// <inheritdoc />
    public event EventHandler<ChannelHealthChangedEventArgs>? HealthChanged;

    /// <inheritdoc />
    public event EventHandler<IncomingCallEventArgs>? IncomingCall;

    /// <inheritdoc />
    public int ActiveCalls => Volatile.Read(ref _activeCalls);

    /// <inheritdoc />
    public async ValueTask QuiesceAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _quiesced, 1) != 0)
        {
            return;
        }

        // Withdrawing the registration is what actually stops the traffic: the carrier stops routing
        // to this line instead of us rejecting call after call. The reject path below only covers the
        // race — an INVITE already on the wire when we unregistered.
        try
        {
            await _line.UnregisterAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Channel {ChannelId} quiesced; its SIP registration was withdrawn with {ActiveCalls} call(s) still up.",
                ChannelId,
                ActiveCalls);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A line that cannot unregister is still quiesced: the flag is set, so inbound calls are
            // refused from here on. Worth reporting, not worth failing the drain over.
            _logger.LogWarning(
                ex, "Channel {ChannelId} could not withdraw its SIP registration while quiescing.", ChannelId);
        }
    }

    /// <inheritdoc />
    public async Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

        // A quiesced channel refuses in both directions. Dialing out of a line whose registration is
        // gone would fail at the carrier anyway; failing here says why.
        if (Volatile.Read(ref _quiesced) != 0)
        {
            throw new InvalidOperationException(
                $"Channel '{ChannelId}' is draining and does not accept new calls.");
        }

        // Reserve a slot atomically before dialing; release on Terminated or on Dial failure.
        if (Interlocked.Increment(ref _activeCalls) > _maxConcurrentCalls)
        {
            Interlocked.Decrement(ref _activeCalls);
            throw new InvalidOperationException(
                $"Channel '{ChannelId}' has reached its concurrent-call limit of {_maxConcurrentCalls}.");
        }

        try
        {
            var sdkCall = await _line.DialAsync(target.Value, options: null, cancellationToken).ConfigureAwait(false);
            var call = new SdkCall(sdkCall, _mediaTapFactory);
            TrackForCapacity(call);
            return call;
        }
        catch
        {
            // Dial failure: release the reservation so subsequent calls are not blocked.
            Interlocked.Decrement(ref _activeCalls);
            throw;
        }
    }

    private void OnSdkIncomingCall(object? sender, SdkIncomingCallEventArgs e)
    {
        // Draining: this INVITE was already on the wire when the registration went away. Answering it
        // would restart the clock on a channel that is trying to run empty.
        if (Volatile.Read(ref _quiesced) != 0)
        {
            _ = RejectQuiescedAsync(e.Call);
            return;
        }

        var handler = IncomingCall;
        if (handler is null)
        {
            return;
        }

        // Inbound calls count against the concurrent-call ceiling so outbound gating reflects the real
        // trunk load — and one the account has no line for is refused instead of counted. Counting it
        // anyway made the ceiling one-sided: outbound was blocked while inbound kept arriving, so the
        // trunk ran over the limit an operator had set. Reserved the same way as a dial, because
        // twenty INVITEs at once is a Monday morning and not a stress test.
        if (Interlocked.Increment(ref _activeCalls) > _maxConcurrentCalls)
        {
            Interlocked.Decrement(ref _activeCalls);
            _ = RejectAtCapacityAsync(e.Call);
            return;
        }

        // Wrap the SDK call so consumers only ever see the foundation contract (and can open audio).
        var call = new SdkCall(e.Call, _mediaTapFactory);
        TrackForCapacity(call);
        handler(this, new IncomingCallEventArgs(call));
    }

    /// <summary>
    /// Subscribes to <paramref name="call"/>'s <c>StateChanged</c> event and decrements
    /// <see cref="_activeCalls"/> exactly once when the call reaches
    /// <see cref="CallState.Terminated"/>. Uses a closure-local flag for idempotency so
    /// a duplicate Terminated event (or a race where the call is already terminated at subscribe time)
    /// does not decrement twice.
    /// Note: if the channel is disposed while calls are still live, their capacity-tracking handlers
    /// remain subscribed on the call objects until those calls themselves terminate — this is acceptable
    /// in v1 because the channel is gone and no further dials are possible.
    /// </summary>
    private void TrackForCapacity(SdkCall call)
    {
        // Already terminated before we could subscribe: decrement immediately.
        if (call.State == CallState.Terminated)
        {
            Interlocked.Decrement(ref _activeCalls);
            return;
        }

        // One-shot idempotency flag shared by the closure (0 = not yet fired, 1 = fired).
        var fired = new[] { 0 };

        void OnStateChanged(object? sender, CallStateChangedEventArgs e)
        {
            if (e.CurrentState != CallState.Terminated)
            {
                return;
            }

            if (Interlocked.Exchange(ref fired[0], 1) == 0)
            {
                Interlocked.Decrement(ref _activeCalls);
            }

            call.StateChanged -= OnStateChanged;
        }

        call.StateChanged += OnStateChanged;

        // Re-check after subscribing to close the race: the call may have terminated between the
        // initial check and the subscribe.
        if (call.State == CallState.Terminated)
        {
            if (Interlocked.Exchange(ref fired[0], 1) == 0)
            {
                Interlocked.Decrement(ref _activeCalls);
            }

            call.StateChanged -= OnStateChanged;
        }
    }

    /// <summary>
    /// Turns an INVITE that arrived during the drain away with 503. Fire-and-forget on purpose: the
    /// SDK raises the arrival synchronously, and nothing downstream is waiting on the answer.
    /// </summary>
    private async Task RejectQuiescedAsync(CalloraVoipSdk.Core.Domain.Calls.ICall call)
    {
        if (await TryRejectAsync(call, ServiceUnavailableStatus, "Service Unavailable").ConfigureAwait(false))
        {
            _logger.LogInformation(
                "Channel {ChannelId} refused an inbound call with {Status} while draining.",
                ChannelId,
                ServiceUnavailableStatus);
        }
    }

    /// <summary>
    /// Turns an INVITE away that arrived with every line busy. Fire-and-forget for the same reason as
    /// the drain: the SDK raises the arrival synchronously.
    /// </summary>
    private async Task RejectAtCapacityAsync(CalloraVoipSdk.Core.Domain.Calls.ICall call)
    {
        if (await TryRejectAsync(call, BusyHereStatus, "Busy Here").ConfigureAwait(false))
        {
            // Worth an operator's attention rather than a debug line: this is what "the trunk is too
            // small" looks like from the inside.
            _logger.LogInformation(
                "Channel {ChannelId} refused an inbound call with {Status}: all {Limit} line(s) are in use.",
                ChannelId,
                BusyHereStatus,
                _maxConcurrentCalls);
        }
    }

    /// <summary>
    /// Refuses one call, reporting whether it worked. The caller hearing nothing is bad; a background
    /// exception tearing down the process is worse, and the carrier times the invitation out either way.
    /// </summary>
    private async Task<bool> TryRejectAsync(CalloraVoipSdk.Core.Domain.Calls.ICall call, int status, string reason)
    {
        try
        {
            await call.RejectAsync(status, reason).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex, "Channel {ChannelId} failed to refuse an inbound call with {Status}.", ChannelId, status);
            return false;
        }
    }

    private void OnLineStateChanged(object? sender, SdkLineStateChangedEventArgs e)
    {
        var previous = MapHealth(e.OldState);
        var current = MapHealth(e.NewState);
        if (previous != current)
        {
            HealthChanged?.Invoke(this, new ChannelHealthChangedEventArgs(current));
        }
    }

    private static ChannelHealth MapHealth(LineState state) => state switch
    {
        LineState.Registered => ChannelHealth.Up,
        LineState.Reconnecting or LineState.RegistrationFailed => ChannelHealth.Degraded,
        LineState.Failed => ChannelHealth.Down,
        // Unregistered / Registering (and any future state) — not yet usable, but not a hard failure.
        _ => ChannelHealth.Unknown,
    };

    /// <summary>Unsubscribes from the line so the channel does not outlive its registration.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _line.IncomingCall -= OnSdkIncomingCall;
        _line.StateChanged -= OnLineStateChanged;
    }
}
