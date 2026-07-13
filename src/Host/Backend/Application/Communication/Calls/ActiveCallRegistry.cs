using System.Collections.Concurrent;
using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// Tracks live calls per workspace and publishes their lifecycle to the
/// <see cref="CallEventBroadcaster"/>. Calls remove themselves on termination.
/// </summary>
public sealed class ActiveCallRegistry(CallEventBroadcaster broadcaster)
{
    private readonly ConcurrentDictionary<string, TrackedCall> _calls = new(StringComparer.OrdinalIgnoreCase);

    public ActiveCallSnapshot TrackIncoming(string workspaceKey, string channelId, ICall call) =>
        Track(workspaceKey, channelId, call, CallEventTypes.Ringing);

    public ActiveCallSnapshot TrackPlaced(string workspaceKey, string channelId, ICall call) =>
        Track(workspaceKey, channelId, call, CallEventTypes.Placed);

    /// <summary>
    /// All live calls across workspaces — used by the graceful-shutdown
    /// path to hang up remaining calls (PLAT-234).
    /// </summary>
    public IReadOnlyList<TrackedCall> ListAllTracked() => [.. _calls.Values];

    public IReadOnlyList<ActiveCallSnapshot> List(string workspaceKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
            return [];

        return _calls.Values
            .Where(tracked => string.Equals(tracked.WorkspaceKey, workspaceKey.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(static tracked => tracked.StartedAtUtc)
            .Select(static tracked => tracked.ToSnapshot())
            .ToArray();
    }

    /// <summary>
    /// Resolves one tracked call, scoped to its workspace so callers cannot
    /// reach calls of foreign workspaces by identifier.
    /// </summary>
    public bool TryGet(string workspaceKey, string callId, out TrackedCall? tracked)
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

    private ActiveCallSnapshot Track(string workspaceKey, string channelId, ICall call, string eventType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentNullException.ThrowIfNull(call);

        var tracked = new TrackedCall(workspaceKey.Trim(), channelId, call, DateTimeOffset.UtcNow);
        if (!_calls.TryAdd(call.CallId, tracked))
        {
            // Terminated race: the existing entry may vanish concurrently.
            return _calls.TryGetValue(call.CallId, out var existing)
                ? existing.ToSnapshot()
                : tracked.ToSnapshot();
        }

        CallTelemetry.RecordStarted(tracked.WorkspaceKey, DirectionTag(call));

        EventHandler<CallStateChangedEventArgs>? handler = null;
        handler = (_, args) => HandleStateChanged(tracked, handler!, args);
        call.StateChanged += handler;

        // The call may have terminated between placement and subscription;
        // drop it immediately instead of leaking a dead entry.
        if (call.State == CallState.Terminated)
        {
            RemoveTerminated(tracked, handler);
            return tracked.ToSnapshot();
        }

        broadcaster.Publish(new CallEvent(eventType, tracked.ToSnapshot()));
        return tracked.ToSnapshot();
    }

    private void HandleStateChanged(
        TrackedCall tracked,
        EventHandler<CallStateChangedEventArgs> handler,
        CallStateChangedEventArgs args)
    {
        if (args.CurrentState == CallState.Terminated)
        {
            RemoveTerminated(tracked, handler);
            return;
        }

        broadcaster.Publish(new CallEvent(CallEventTypes.StateChanged, tracked.ToSnapshot()));
    }

    private void RemoveTerminated(TrackedCall tracked, EventHandler<CallStateChangedEventArgs> handler)
    {
        // Detach so long-lived channel/call objects cannot pin tracked entries.
        tracked.Call.StateChanged -= handler;

        if (_calls.TryRemove(tracked.Call.CallId, out _))
        {
            CallTelemetry.RecordEnded(
                tracked.WorkspaceKey,
                DirectionTag(tracked.Call),
                DateTimeOffset.UtcNow - tracked.StartedAtUtc);
            broadcaster.Publish(new CallEvent(CallEventTypes.Ended, tracked.ToSnapshot()));
        }
    }

    private static string DirectionTag(ICall call) => call.Direction.ToString().ToLowerInvariant();
}
