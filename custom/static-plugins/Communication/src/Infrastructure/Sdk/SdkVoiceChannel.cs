using Callora.Plugin.Communication.Abstractions;
using CalloraVoipSdk.Core.Application.Media;
using CalloraVoipSdk.Core.Domain.Lines;
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
public sealed class SdkVoiceChannel : IVoiceChannel, IDisposable
{
    private static readonly IReadOnlyCollection<string> VoiceCapability = [CommunicationCapabilities.Voice];

    private readonly IPhoneLine _line;
    private readonly Func<(IMediaReceiver Receiver, IMediaSender Sender)> _mediaTapFactory;
    private readonly int _maxConcurrentCalls;
    private int _activeCalls;
    private int _disposed;

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
        int maxConcurrentCalls)
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
    public async Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);

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
        var handler = IncomingCall;
        if (handler is null)
        {
            return;
        }

        // Inbound calls count against the concurrent-call ceiling so outbound gating reflects the real
        // trunk load.  Inbound calls are NOT rejected here (requires a SDK-level reject path, follow-up).
        Interlocked.Increment(ref _activeCalls);

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
