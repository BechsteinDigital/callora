using System.Collections.Concurrent;
using Callora.Core.Application.Events.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Default <see cref="ICallControlService"/>: resolves the workspace's voice channel from the
/// <see cref="ICommunicationChannelRegistry"/>, places the call, tracks it channel-neutrally via
/// <see cref="ICall"/>, records <see cref="CallLog"/> history and publishes <c>call.*</c> business
/// events on each lifecycle transition. It owns no dialer/PBX/agent behaviour — that lives in the
/// plugins built on top of this primitive.
/// </summary>
public sealed class CallControlService : ICallControlService, IDisposable
{
    private readonly ICommunicationChannelRegistry _channels;
    private readonly ICallLogStore _callLogStore;
    private readonly IBusinessEventBus? _eventBus;
    private readonly ILogger<CallControlService> _logger;
    private readonly TimeProvider _timeProvider;

    // Keyed by callId (unique per channel). WorkspaceKey on the entry scopes hangup/get so one
    // workspace can never touch another's call.
    private readonly ConcurrentDictionary<string, TrackedCall> _active = new(StringComparer.Ordinal);

    /// <summary>Creates the service over the channel registry, call-log store and (optional) event bus.</summary>
    public CallControlService(
        ICommunicationChannelRegistry channels,
        ICallLogStore callLogStore,
        IBusinessEventBus? eventBus,
        ILogger<CallControlService> logger,
        TimeProvider timeProvider)
    {
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _callLogStore = callLogStore ?? throw new ArgumentNullException(nameof(callLogStore));
        _eventBus = eventBus;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<CallSnapshot> PlaceCallAsync(PlaceCallCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var channel = ResolveChannel(command);
        var call = await channel
            .PlaceCallAsync(new CallTarget(command.To, command.DisplayName), cancellationToken)
            .ConfigureAwait(false);

        var startedAt = _timeProvider.GetUtcNow();
        var log = CallLog.Start(
            id: call.CallId,
            workspaceKey: command.WorkspaceKey,
            accountId: channel.ChannelId,
            lineId: null,
            direction: CallDirection.Outbound,
            remoteParty: command.To,
            localIdentity: channel.DisplayName,
            handledBy: null,
            correlationId: null,
            startedAt: startedAt);
        await _callLogStore.AddAsync(log, cancellationToken).ConfigureAwait(false);

        void Handler(object? sender, CallStateChangedEventArgs e) => _ = HandleStateChangeAsync(call.CallId, e.CurrentState);
        _active[call.CallId] = new TrackedCall(command.WorkspaceKey, call, log, Handler);

        // Publish placed before wiring the lifecycle so consumers always observe placed ahead of any
        // state-changed/ended, even for a call that terminates the instant it is placed.
        await PublishAsync(
            CallBusinessEvent.Placed(command.WorkspaceKey, call.CallId, CallDirection.Outbound, command.To, call.State, startedAt),
            cancellationToken).ConfigureAwait(false);

        call.StateChanged += Handler;

        // Race: the call may have advanced past Connecting before we subscribed. Re-evaluate once so a
        // fast Connected/Terminated is not missed; the handlers are idempotent against a double fire.
        if (call.State is CallState.Connected or CallState.Terminated)
        {
            await HandleStateChangeAsync(call.CallId, call.State).ConfigureAwait(false);
        }

        return Snapshot(call);
    }

    /// <inheritdoc />
    public async Task<bool> HangupAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default)
    {
        if (!TryGetOwned(workspaceKey, callId, out var tracked))
        {
            return false;
        }

        // The resulting Terminated transition drives OnTerminatedAsync, which finalizes the log.
        await tracked.Call.HangupAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public CallSnapshot? Get(string workspaceKey, string callId) =>
        TryGetOwned(workspaceKey, callId, out var tracked) ? Snapshot(tracked.Call) : null;

    /// <summary>Detaches every live handler so no tracked call outlives the service (plugin stop/unload).</summary>
    public void Dispose()
    {
        foreach (var tracked in _active.Values)
        {
            tracked.Call.StateChanged -= tracked.Handler;
        }

        _active.Clear();
    }

    private ICommunicationChannel ResolveChannel(PlaceCallCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.ChannelId))
        {
            return _channels.TryGetChannel(command.WorkspaceKey, command.ChannelId, out var byId) && byId is not null
                ? byId
                : throw new InvalidOperationException(
                    $"No channel '{command.ChannelId}' is registered for workspace '{command.WorkspaceKey}'.");
        }

        var voiceChannels = _channels.GetChannelsByCapability(command.WorkspaceKey, CommunicationCapabilities.Voice);
        return voiceChannels.Count > 0
            ? voiceChannels[0]
            : throw new InvalidOperationException(
                $"No voice-capable channel is registered for workspace '{command.WorkspaceKey}'.");
    }

    private async Task HandleStateChangeAsync(string callId, CallState state)
    {
        try
        {
            switch (state)
            {
                case CallState.Connected:
                    await OnConnectedAsync(callId).ConfigureAwait(false);
                    break;
                case CallState.Terminated:
                    await OnTerminatedAsync(callId).ConfigureAwait(false);
                    break;
                default:
                    break; // Connecting/Ringing carry no history change for an outbound call.
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record state {State} for call {CallId}.", state, callId);
        }
    }

    private async Task OnConnectedAsync(string callId)
    {
        // Guard against a double fire (handler + race re-check): only the first Connected records.
        if (!_active.TryGetValue(callId, out var tracked) || tracked.Log.AnsweredAt is not null)
        {
            return;
        }

        var answeredAt = _timeProvider.GetUtcNow();
        tracked.Log.MarkAnswered(answeredAt);
        await _callLogStore.UpdateAsync(tracked.Log).ConfigureAwait(false);
        await PublishAsync(
            CallBusinessEvent.StateChanged(
                tracked.WorkspaceKey, callId, tracked.Log.Direction, tracked.Log.RemoteParty, CallState.Connected, answeredAt),
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task OnTerminatedAsync(string callId)
    {
        // TryRemove makes finalization run exactly once even if Terminated fires twice.
        if (!_active.TryRemove(callId, out var tracked))
        {
            return;
        }

        tracked.Call.StateChanged -= tracked.Handler;

        var endedAt = _timeProvider.GetUtcNow();
        // Without a protocol disconnect cause on the neutral ICall, an unanswered outbound call is
        // recorded as Failed (the only non-answered outcome that fits an outbound leg). Enriching this
        // to Busy/NoAnswer needs a disconnect cause on ICall — tracked as a follow-up.
        var outcome = tracked.Log.AnsweredAt is not null ? CallOutcome.Completed : CallOutcome.Failed;
        tracked.Log.End(endedAt, outcome, disconnectCause: null);
        await _callLogStore.UpdateAsync(tracked.Log).ConfigureAwait(false);
        await PublishAsync(
            CallBusinessEvent.Ended(tracked.WorkspaceKey, callId, tracked.Log.Direction, tracked.Log.RemoteParty, endedAt),
            CancellationToken.None).ConfigureAwait(false);
    }

    private bool TryGetOwned(string workspaceKey, string callId, out TrackedCall tracked)
    {
        if (_active.TryGetValue(callId, out var found) &&
            string.Equals(found.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
        {
            tracked = found;
            return true;
        }

        tracked = null!;
        return false;
    }

    private async Task PublishAsync(CallBusinessEvent businessEvent, CancellationToken cancellationToken)
    {
        if (_eventBus is null)
        {
            return;
        }

        try
        {
            await _eventBus.PublishAsync(businessEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Event delivery is a best-effort side effect; a failing bus must not break call control.
            _logger.LogWarning(ex, "Failed to publish {EventName}.", businessEvent.EventName);
        }
    }

    private static CallSnapshot Snapshot(ICall call) =>
        new(call.CallId, call.Direction, call.State, call.Target.Value);
}
