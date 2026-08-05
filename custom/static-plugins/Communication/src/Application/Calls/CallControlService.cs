using System.Collections.Concurrent;
using System.Text.Json;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Calls;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Default <see cref="ICallControlService"/>: resolves the workspace's voice channel from the
/// <see cref="ICommunicationChannelRegistry"/>, places the call, tracks it channel-neutrally via
/// <see cref="ICall"/>, records <see cref="CallLog"/> history and emits <c>call.*</c> business
/// events on each lifecycle transition. It owns no dialer/PBX/agent behaviour — that lives in the
/// plugins built on top of this primitive.
/// <para>
/// Three properties make the tracking trustworthy (#113). Calls are keyed by workspace, channel
/// and call id, because a provider's call id is unique only inside its own channel. Each call's
/// transitions run under its own gate and advance a forward-only stage, so overlapping or
/// reordered provider callbacks cannot interleave into an answered-after-ended history. Events
/// are written to the outbox in the same transaction as the log change they describe, so a bus
/// outage delays delivery instead of losing it.
/// </para>
/// </summary>
public sealed class CallControlService : ICallControlService, IAsyncDisposable
{
    private readonly ICommunicationChannelRegistry _channels;
    private readonly ICallLogStore _callLogStore;
    private readonly ILogger<CallControlService> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<ActiveCallKey, TrackedCall> _active = new();

    /// <summary>Creates the service over the channel registry and call-log store.</summary>
    public CallControlService(
        ICommunicationChannelRegistry channels,
        ICallLogStore callLogStore,
        ILogger<CallControlService> logger,
        TimeProvider timeProvider)
    {
        _channels = channels ?? throw new ArgumentNullException(nameof(channels));
        _callLogStore = callLogStore ?? throw new ArgumentNullException(nameof(callLogStore));
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

        try
        {
            // Log the operator's verbatim target for an outbound call, not the provider's
            // normalized remote party.
            await StartTrackingAsync(
                    command.WorkspaceKey, channel, call, CallDirection.Outbound, command.To, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The call is already live at the carrier. Leaving it up while the API reports a
            // failure would bill the customer for a call nobody can see or hang up, so it is
            // compensated and the attempt is recorded (#113).
            await CompensateUntrackedCallAsync(command, channel, call, ex).ConfigureAwait(false);
            throw;
        }

        return Snapshot(call);
    }

    /// <summary>
    /// Begins tracking one inbound call arriving on a channel: records history, emits
    /// <c>call.ringing</c> and follows its lifecycle. Does not answer or route the call — that is
    /// a consumer's (for example a PBX plugin's) decision. Called by the inbound-call observer,
    /// not by consumers.
    /// </summary>
    public async Task ObserveIncomingAsync(
        string workspaceKey, ICommunicationChannel channel, ICall call, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(call);

        try
        {
            // Invoked fire-and-forget from the channel's IncomingCall event, so a recording
            // failure is logged rather than propagated back into the channel's event dispatch.
            // For an inbound call the remote party is only known from the call itself.
            await StartTrackingAsync(workspaceKey, channel, call, CallDirection.Inbound, call.Target.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to observe inbound call {CallId} on channel {ChannelId}.", call.CallId, channel.ChannelId);
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

    /// <summary>
    /// Finalizes every call still tracked, rather than only detaching handlers (#113). A call
    /// left in progress would stay that way in history forever, because nothing after shutdown
    /// knows it existed.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var key in _active.Keys.ToArray())
        {
            if (!_active.TryGetValue(key, out var tracked))
            {
                continue;
            }

            try
            {
                await FinalizeAsync(tracked, CallOutcome.Failed, "The host shut down while the call was active.")
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Finalizing call {CallId} during shutdown failed.", key.CallId);
                Detach(tracked);
            }
        }

        _active.Clear();
    }

    // Shared tracking for both directions: record the start together with the initial event in
    // one transaction, then wire the lifecycle and re-check once for a call that already advanced
    // past its initial state before the handler was attached.
    private async Task StartTrackingAsync(
        string workspaceKey,
        ICommunicationChannel channel,
        ICall call,
        CallDirection direction,
        string remoteParty,
        CancellationToken cancellationToken)
    {
        var startedAt = _timeProvider.GetUtcNow();
        var key = new ActiveCallKey(workspaceKey, channel.ChannelId, call.CallId);

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

        var initial = direction == CallDirection.Outbound
            ? CallBusinessEvent.Placed(workspaceKey, call.CallId, direction, remoteParty, call.State, startedAt)
            : CallBusinessEvent.Ringing(workspaceKey, call.CallId, direction, remoteParty, call.State, startedAt);

        await _callLogStore.AddAsync(log, ToOutboxEntry(initial, startedAt), cancellationToken).ConfigureAwait(false);

        void Handler(object? sender, CallStateChangedEventArgs e) => _ = HandleStateChangeAsync(key, e.CurrentState);

        var tracked = new TrackedCall(key, call, log, Handler);
        if (!_active.TryAdd(key, tracked))
        {
            // The same call is already tracked (a duplicated inbound notification); keep the
            // first registration rather than replacing a live one.
            tracked.Dispose();
            return;
        }

        call.StateChanged += Handler;
        if (call.State is CallState.Connected or CallState.Terminated)
        {
            await HandleStateChangeAsync(key, call.State).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Hangs up a call that could not be tracked and records the attempt. Best effort by
    /// necessity: if the hangup also fails there is nothing further this process can do, and the
    /// log line is the only evidence an operator will have.
    /// </summary>
    private async Task CompensateUntrackedCallAsync(
        PlaceCallCommand command,
        ICommunicationChannel channel,
        ICall call,
        Exception cause)
    {
        _logger.LogError(
            cause,
            "Tracking the outbound call {CallId} on channel {ChannelId} in workspace {WorkspaceKey} failed; hanging it up.",
            call.CallId,
            channel.ChannelId,
            command.WorkspaceKey);

        try
        {
            await call.HangupAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception hangupFailure)
        {
            _logger.LogError(
                hangupFailure,
                "Compensating hangup for the untracked call {CallId} on channel {ChannelId} failed; the call may still be live.",
                call.CallId,
                channel.ChannelId);
        }
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

    private async Task HandleStateChangeAsync(ActiveCallKey key, CallState state)
    {
        if (state is not (CallState.Connected or CallState.Terminated))
        {
            // Connecting and Ringing carry no history change for either direction.
            return;
        }

        if (!_active.TryGetValue(key, out var tracked))
        {
            return;
        }

        // One call's transitions are serialized; unrelated calls never wait on each other.
        await tracked.Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (state == CallState.Connected)
            {
                await OnConnectedAsync(tracked).ConfigureAwait(false);
            }
            else
            {
                var (outcome, disconnectCause) = ResolveOutcome(tracked);
                await FinalizeAsync(tracked, outcome, disconnectCause).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record state {State} for call {CallId}.", state, key.CallId);
        }
        finally
        {
            tracked.Gate.Release();
        }
    }

    private async Task OnConnectedAsync(TrackedCall tracked)
    {
        // A repeated or late Connected (handler plus the post-subscribe re-check) does not
        // advance the stage, so the answer is recorded exactly once.
        if (!tracked.TryAdvanceTo(CallLifecycleStage.Connected))
        {
            return;
        }

        var answeredAt = _timeProvider.GetUtcNow();
        tracked.Log.MarkAnswered(answeredAt);

        var connected = CallBusinessEvent.StateChanged(
            tracked.WorkspaceKey,
            tracked.Key.CallId,
            tracked.Log.Direction,
            tracked.Log.RemoteParty,
            CallState.Connected,
            answeredAt);
        await _callLogStore.UpdateAsync(tracked.Log, ToOutboxEntry(connected, answeredAt)).ConfigureAwait(false);
    }

    /// <summary>
    /// Ends the call exactly once: advances the stage, detaches the handler, finalizes the log
    /// and enqueues <c>call.ended</c> in the same transaction. Callers hold the call's gate.
    /// </summary>
    private async Task FinalizeAsync(TrackedCall tracked, CallOutcome outcome, string? disconnectCause)
    {
        if (!tracked.TryAdvanceTo(CallLifecycleStage.Terminated))
        {
            return;
        }

        Detach(tracked);

        var endedAt = _timeProvider.GetUtcNow();
        tracked.Log.End(endedAt, outcome, disconnectCause);

        var ended = CallBusinessEvent.Ended(
            tracked.WorkspaceKey, tracked.Key.CallId, tracked.Log.Direction, tracked.Log.RemoteParty, endedAt);
        await _callLogStore.UpdateAsync(tracked.Log, ToOutboxEntry(ended, endedAt)).ConfigureAwait(false);
    }

    /// <summary>Removes the call from tracking and unhooks its handler. Safe to call twice.</summary>
    private void Detach(TrackedCall tracked)
    {
        if (_active.TryRemove(tracked.Key, out _))
        {
            tracked.Call.StateChanged -= tracked.Handler;
            tracked.Dispose();
        }
    }

    /// <summary>
    /// Wraps a business event as an outbox entry. The payload is the event's own data, so the
    /// drainer republishes exactly what an in-process listener would have seen.
    /// </summary>
    private static CallEventOutboxEntry ToOutboxEntry(CallBusinessEvent businessEvent, DateTimeOffset occurredAt) =>
        CallEventOutboxEntry.Pending(
            Guid.NewGuid(),
            businessEvent.EventName,
            businessEvent.WorkspaceKey ?? string.Empty,
            JsonSerializer.Serialize(businessEvent.ToEventData(), PayloadOptions),
            occurredAt);

    // Derives the terminal outcome and disconnect cause, reconciled with whether the call was
    // answered (CallLog.End enforces answered→{Completed,Failed}, unanswered→{Missed,Rejected,
    // Busy,NoAnswer,Canceled,Failed}). Uses the provider's termination reason when present;
    // otherwise falls back to the coarse heuristic.
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

    /// <summary>
    /// Resolves a call the workspace owns. Scans by workspace and call id because a consumer
    /// names the call, not the channel it happens to run on; the channel is part of the tracking
    /// key so two channels' identical call ids stay distinct entries.
    /// </summary>
    private bool TryGetOwned(string workspaceKey, string callId, out TrackedCall tracked)
    {
        foreach (var (key, candidate) in _active)
        {
            if (string.Equals(key.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(key.CallId, callId, StringComparison.Ordinal))
            {
                tracked = candidate;
                return true;
            }
        }

        tracked = null!;
        return false;
    }

    private static CallSnapshot Snapshot(ICall call) =>
        new(call.CallId, call.Direction, call.State, call.Target.Value);
}
