using System.Collections.Concurrent;
using System.Linq;
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

        // Log the operator's verbatim target for an outbound call (not the SDK's normalized remote party).
        await StartTrackingAsync(command.WorkspaceKey, channel, call, CallDirection.Outbound, command.To, cancellationToken)
            .ConfigureAwait(false);
        return Snapshot(call);
    }

    /// <summary>
    /// Begins tracking one inbound call arriving on a channel: records history, publishes
    /// <c>call.ringing</c> and follows its lifecycle. Does not answer or route the call — that is a
    /// consumer's (e.g. a PBX plugin's) decision. Called by the inbound-call observer, not consumers.
    /// </summary>
    public async Task ObserveIncomingAsync(
        string workspaceKey, ICommunicationChannel channel, ICall call, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            // Invoked fire-and-forget from the channel's IncomingCall event — swallow and log failures
            // so a recording error never propagates back into the channel's event dispatch.
            // For an inbound call the remote party is only known from the call itself.
            await StartTrackingAsync(workspaceKey, channel, call, CallDirection.Inbound, call.Target.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to observe inbound call {CallId} on channel {ChannelId}.", call.CallId, channel.ChannelId);
        }
    }

    // Shared tracking for both directions: record the start, publish the initial event before wiring
    // the lifecycle (so consumers see placed/ringing ahead of any state-changed/ended), then re-check
    // once for a call that already advanced past its initial state before we subscribed.
    private async Task StartTrackingAsync(
        string workspaceKey, ICommunicationChannel channel, ICall call, CallDirection direction, string remoteParty, CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var log = CallLog.Start(
            id: call.CallId,
            workspaceKey: workspaceKey,
            accountId: channel.ChannelId,
            lineId: null,
            direction: direction,
            remoteParty: remoteParty,
            localIdentity: channel.DisplayName,
            handledBy: null,
            correlationId: null,
            startedAt: startedAt);
        await _callLogStore.AddAsync(log, cancellationToken).ConfigureAwait(false);

        void Handler(object? sender, CallStateChangedEventArgs e) => _ = HandleStateChangeAsync(call.CallId, e.CurrentState);
        _active[call.CallId] = new TrackedCall(workspaceKey, call, log, Handler);

        var initial = direction == CallDirection.Outbound
            ? CallBusinessEvent.Placed(workspaceKey, call.CallId, direction, remoteParty, call.State, startedAt)
            : CallBusinessEvent.Ringing(workspaceKey, call.CallId, direction, remoteParty, call.State, startedAt);
        await PublishAsync(initial, cancellationToken).ConfigureAwait(false);

        call.StateChanged += Handler;
        if (call.State is CallState.Connected or CallState.Terminated)
        {
            await HandleStateChangeAsync(call.CallId, call.State).ConfigureAwait(false);
        }
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(
        string workspaceKey, int limit, CancellationToken cancellationToken = default)
    {
        var logs = await _callLogStore.ListRecentAsync(workspaceKey, limit, cancellationToken).ConfigureAwait(false);
        return [.. logs.Select(CallHistoryEntryMapper.FromDomain)];
    }

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
                    break; // Connecting/Ringing carry no history change for either direction.
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
        var (outcome, disconnectCause) = ResolveOutcome(tracked);
        tracked.Log.End(endedAt, outcome, disconnectCause);
        await _callLogStore.UpdateAsync(tracked.Log).ConfigureAwait(false);
        await PublishAsync(
            CallBusinessEvent.Ended(tracked.WorkspaceKey, callId, tracked.Log.Direction, tracked.Log.RemoteParty, endedAt),
            CancellationToken.None).ConfigureAwait(false);
    }

    // Derives the terminal outcome + disconnect cause, reconciled with whether the call was answered
    // (CallLog.End enforces answered→{Completed,Failed}, unanswered→{Missed,Rejected,Busy,NoAnswer,
    // Canceled,Failed}). Uses the SDK-supplied termination reason when present; otherwise falls back
    // to the coarse heuristic (answered→Completed, unanswered inbound→Missed / outbound→Failed).
    private static (CallOutcome Outcome, string? DisconnectCause) ResolveOutcome(TrackedCall tracked)
    {
        var wasAnswered = tracked.Log.AnsweredAt is not null;
        var reason = tracked.Call.TerminationReason;

        if (reason is null)
        {
            var fallback = wasAnswered
                ? CallOutcome.Completed
                : tracked.Log.Direction == CallDirection.Inbound ? CallOutcome.Missed : CallOutcome.Failed;
            return (fallback, null);
        }

        var outcome = wasAnswered
            // An answered call can only end Completed or Failed; anything else is a protocol anomaly → Failed.
            ? reason.Category == CallTerminationCategory.Completed ? CallOutcome.Completed : CallOutcome.Failed
            : MapUnansweredOutcome(reason.Category, tracked.Log.Direction);

        return (outcome, DescribeDisconnectCause(reason));
    }

    private static CallOutcome MapUnansweredOutcome(CallTerminationCategory category, CallDirection direction) => category switch
    {
        CallTerminationCategory.Busy => CallOutcome.Busy,
        CallTerminationCategory.NoAnswer => CallOutcome.NoAnswer,
        CallTerminationCategory.Rejected => CallOutcome.Rejected,
        CallTerminationCategory.Canceled => CallOutcome.Canceled,
        CallTerminationCategory.Failed => CallOutcome.Failed,
        // An unanswered call reported "Completed" is untypical; map to the safe unanswered equivalent.
        CallTerminationCategory.Completed => direction == CallDirection.Inbound ? CallOutcome.Missed : CallOutcome.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown termination category."),
    };

    // Compact protocol detail for the DisconnectCause column (varchar(200)): "486 Busy Here",
    // else the reason phrase, else null.
    private static string? DescribeDisconnectCause(CallTerminationReason reason)
    {
        var cause = (reason.SipStatusCode, reason.ReasonPhrase) switch
        {
            (int code, { Length: > 0 } phrase) => $"{code} {phrase}",
            (int code, _) => code.ToString(),
            (null, { Length: > 0 } phrase) => phrase,
            _ => null,
        };

        return cause is { Length: > 200 } ? cause[..200] : cause;
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
