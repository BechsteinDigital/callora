namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Channel-neutral call-control primitive: the single seam through which any consumer — an in-process
/// plugin (Dialer/PBX/CRM) via DI or an out-of-process client via the REST adapter — places and controls
/// calls. It resolves the workspace's voice channel, tracks the live call, records call history and
/// publishes <c>call.*</c> business events. It owns no domain logic (no dialer/PBX/agent behaviour);
/// those live in their own plugins on top of this primitive.
/// </summary>
public interface ICallControlService
{
    /// <summary>
    /// Places one outbound call. The returned snapshot reflects the call's initial state; further
    /// transitions are observed internally (call history + <c>call.*</c> events). Throws
    /// <see cref="InvalidOperationException"/> when no voice-capable channel is available.
    /// </summary>
    Task<CallSnapshot> PlaceCallAsync(PlaceCallCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends a live call owned by the workspace. Returns <c>false</c> when no such live call is tracked
    /// (already ended or never known); <c>true</c> once the hang-up was requested.
    /// </summary>
    Task<bool> HangupAsync(string workspaceKey, string callId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a snapshot of a live call owned by the workspace, or <c>null</c> when it is not tracked.
    /// </summary>
    CallSnapshot? Get(string workspaceKey, string callId);

    /// <summary>
    /// Returns the most recent recorded calls for the workspace, newest first, capped at <paramref name="limit"/>.
    /// </summary>
    Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(string workspaceKey, int limit, CancellationToken cancellationToken = default);
}
