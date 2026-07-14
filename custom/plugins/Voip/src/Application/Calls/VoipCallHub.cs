using System.Collections.Concurrent;
using Callora.Contracts.Communication;
using Microsoft.Extensions.Logging;

namespace Callora.Plugins.Voip.Application.Calls;

/// <summary>
/// The plugin-owned call stack (PLAT-257): tracks live calls per workspace,
/// publishes their lifecycle as <see cref="CallStreamEvent"/>s, attaches to
/// channel registrations for inbound calls and places outbound calls. The
/// host carries no call logic — it consumes the exported
/// <see cref="ICallDirectory"/>/<see cref="ICallEventStream"/> contracts.
/// </summary>
public sealed class VoipCallHub(
    ICommunicationChannelRegistry channelRegistry,
    ILogger? logger = null) : ICallDirectory, ICallEventStream
{
    private static readonly TimeSpan ShutdownHangupTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, VoipTrackedCall> _calls = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, VoipCallEventSubscription> _subscriptions = new();
    private readonly ConcurrentDictionary<ICommunicationChannel, EventHandler<IncomingCallEventArgs>> _channelHandlers = new();

    public event Action<CallStreamEvent>? EventPublished;

    /// <summary>
    /// Attaches to the channel registry: existing and future channels feed
    /// their inbound calls into the hub.
    /// </summary>
    public void AttachToChannels()
    {
        channelRegistry.ChannelRegistered += AttachChannel;
        channelRegistry.ChannelUnregistered += DetachChannel;

        foreach (var (workspaceKey, channel) in channelRegistry.GetAllRegistrations())
        {
            AttachChannel(workspaceKey, channel);
        }
    }

    /// <summary>
    /// Graceful shutdown: hangs up remaining calls with a bounded timeout
    /// and completes all subscriptions so streams end cleanly.
    /// </summary>
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        channelRegistry.ChannelRegistered -= AttachChannel;
        channelRegistry.ChannelUnregistered -= DetachChannel;
        foreach (var channel in _channelHandlers.Keys.ToArray())
        {
            DetachChannel(string.Empty, channel);
        }

        var trackedCalls = _calls.Values.ToArray();
        if (trackedCalls.Length > 0)
        {
            logger?.LogInformation("Voice plugin shutdown: hanging up {CallCount} active calls.", trackedCalls.Length);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ShutdownHangupTimeout);

            foreach (var tracked in trackedCalls)
            {
                try
                {
                    if (tracked.Call.State != CallState.Terminated)
                    {
                        await tracked.Call.HangupAsync(timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    logger?.LogWarning(exception, "Hangup for call {CallId} during shutdown failed.", tracked.Call.CallId);
                }
            }
        }

        foreach (var subscription in _subscriptions.Values)
        {
            subscription.Complete();
        }
    }

    public CallSummary TrackIncoming(string workspaceKey, string channelId, ICall call) =>
        Track(workspaceKey, channelId, call, CallEventTypes.Ringing);

    public CallSummary TrackPlaced(string workspaceKey, string channelId, ICall call) =>
        Track(workspaceKey, channelId, call, CallEventTypes.Placed);

    public IReadOnlyList<CallSummary> List(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
            return [];

        return _calls.Values
            .Where(tracked => string.Equals(tracked.WorkspaceKey, workspaceKey.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(static tracked => tracked.StartedAtUtc)
            .Select(static tracked => tracked.ToSummary())
            .ToArray();
    }

    /// <summary>Resolves the tracked call including its summary projection.</summary>
    public bool TryGetTracked(string workspaceKey, string callId, out VoipTrackedCall? tracked)
    {
        tracked = null;
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(callId))
            return false;

        if (!_calls.TryGetValue(callId, out var candidate))
            return false;

        if (!string.Equals(candidate.WorkspaceKey, workspaceKey.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        tracked = candidate;
        return true;
    }

    public bool TryGet(string workspaceKey, string callId, out ICall? call)
    {
        call = null;
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(callId))
            return false;

        if (!_calls.TryGetValue(callId, out var candidate))
            return false;

        if (!string.Equals(candidate.WorkspaceKey, workspaceKey.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        call = candidate.Call;
        return true;
    }

    public async Task<CallSummary> PlaceCallAsync(
        string workspaceKey,
        string? channelId,
        CallTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(target);

        var channel = ResolveChannel(workspaceKey, channelId);
        try
        {
            var call = await channel.PlaceCallAsync(target, cancellationToken).ConfigureAwait(false);
            logger?.LogInformation(
                "Placed call {CallId} on channel {ChannelId} (workspace {WorkspaceKey}).",
                call.CallId,
                channel.ChannelId,
                workspaceKey);
            return TrackPlaced(workspaceKey, channel.ChannelId, call);
        }
        catch (Exception exception)
        {
            // Rufnummer bleibt bewusst außerhalb des Logs (PII).
            logger?.LogWarning(
                exception,
                "Placing a call on channel {ChannelId} (workspace {WorkspaceKey}) failed.",
                channel.ChannelId,
                workspaceKey);
            throw;
        }
    }

    public ICallEventSubscription Subscribe(string workspaceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var subscription = new VoipCallEventSubscription(Guid.NewGuid(), workspaceKey.Trim(), RemoveSubscription);
        _subscriptions[subscription.Id] = subscription;
        return subscription;
    }

    private ICommunicationChannel ResolveChannel(string workspaceKey, string? channelId)
    {
        if (!string.IsNullOrWhiteSpace(channelId))
        {
            if (!channelRegistry.TryGetChannel(workspaceKey, channelId, out var channel) || channel is null)
            {
                throw new InvalidOperationException(
                    $"Channel '{channelId}' is not registered for workspace '{workspaceKey}'.");
            }

            return channel;
        }

        var voiceChannels = channelRegistry.GetChannelsByCapability(workspaceKey, CommunicationCapabilities.Voice);
        return voiceChannels.Count > 0
            ? voiceChannels[0]
            : throw new InvalidOperationException(
                $"No voice channel is registered for workspace '{workspaceKey}'.");
    }

    private void AttachChannel(string workspaceKey, ICommunicationChannel channel)
    {
        EventHandler<IncomingCallEventArgs> handler = (_, args) =>
        {
            logger?.LogInformation(
                "Incoming call {CallId} on channel {ChannelId} (workspace {WorkspaceKey}).",
                args.Call.CallId,
                channel.ChannelId,
                workspaceKey);
            TrackIncoming(workspaceKey, channel.ChannelId, args.Call);
        };

        if (_channelHandlers.TryAdd(channel, handler))
        {
            channel.IncomingCall += handler;
        }
    }

    private void DetachChannel(string workspaceKey, ICommunicationChannel channel)
    {
        if (_channelHandlers.TryRemove(channel, out var handler))
        {
            channel.IncomingCall -= handler;
        }
    }

    private CallSummary Track(string workspaceKey, string channelId, ICall call, string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentNullException.ThrowIfNull(call);

        var tracked = new VoipTrackedCall(workspaceKey.Trim(), channelId, call, DateTimeOffset.UtcNow);
        if (!_calls.TryAdd(call.CallId, tracked))
        {
            // Terminated race: the existing entry may vanish concurrently.
            return _calls.TryGetValue(call.CallId, out var existing)
                ? existing.ToSummary()
                : tracked.ToSummary();
        }

        VoipCallTelemetry.RecordStarted(tracked.WorkspaceKey, DirectionTag(call));

        EventHandler<CallStateChangedEventArgs>? handler = null;
        handler = (_, args) => HandleStateChanged(tracked, handler!, args);
        call.StateChanged += handler;

        // Consent-fähige Calls speisen die Flow-Events call.consent-* (PLAT-241).
        if (call is IRecordingConsentCall consentCall)
        {
            consentCall.ConsentChanged += (_, args) => HandleConsentChanged(tracked, args);
        }

        // The call may have terminated between placement and subscription;
        // drop it immediately instead of leaking a dead entry.
        if (call.State == CallState.Terminated)
        {
            RemoveTerminated(tracked, handler);
            return tracked.ToSummary();
        }

        Publish(new CallStreamEvent(eventType, tracked.ToSummary()));
        return tracked.ToSummary();
    }

    private void HandleConsentChanged(VoipTrackedCall tracked, RecordingConsentChangedEventArgs args)
    {
        var eventType = args.CurrentState switch
        {
            RecordingConsentState.Granted => CallEventTypes.ConsentGranted,
            RecordingConsentState.Denied => CallEventTypes.ConsentDenied,
            _ => null
        };

        if (eventType is not null)
        {
            Publish(new CallStreamEvent(eventType, tracked.ToSummary()));
        }
    }

    private void HandleStateChanged(
        VoipTrackedCall tracked,
        EventHandler<CallStateChangedEventArgs> handler,
        CallStateChangedEventArgs args)
    {
        if (args.CurrentState == CallState.Terminated)
        {
            RemoveTerminated(tracked, handler);
            return;
        }

        Publish(new CallStreamEvent(CallEventTypes.StateChanged, tracked.ToSummary()));
    }

    private void RemoveTerminated(VoipTrackedCall tracked, EventHandler<CallStateChangedEventArgs> handler)
    {
        // Detach so long-lived channel/call objects cannot pin tracked entries.
        tracked.Call.StateChanged -= handler;

        if (_calls.TryRemove(tracked.Call.CallId, out _))
        {
            VoipCallTelemetry.RecordEnded(
                tracked.WorkspaceKey,
                DirectionTag(tracked.Call),
                DateTimeOffset.UtcNow - tracked.StartedAtUtc);
            Publish(new CallStreamEvent(CallEventTypes.Ended, tracked.ToSummary()));
        }
    }

    private void Publish(CallStreamEvent callEvent)
    {
        EventPublished?.Invoke(callEvent);

        foreach (var subscription in _subscriptions.Values)
        {
            if (string.Equals(subscription.WorkspaceKey, callEvent.Call.WorkspaceKey, StringComparison.OrdinalIgnoreCase))
            {
                subscription.Write(callEvent);
            }
        }
    }

    private void RemoveSubscription(Guid subscriptionId) => _subscriptions.TryRemove(subscriptionId, out _);

    private static string DirectionTag(ICall call) => call.Direction.ToString().ToLowerInvariant();
}
