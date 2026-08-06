using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Calls;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// One live call tracked by <see cref="CallControlService"/>: the underlying <see cref="ICall"/>,
/// its persisted <see cref="CallLog"/>, the state-changed handler kept so it can be detached on
/// teardown, and the gate that serializes the call's transitions (#113).
/// <para>
/// Provider callbacks arrive on the SIP stack's threads and can overlap or reorder. Without a
/// per-call gate, Connected and Terminated could interleave into a log that is answered after it
/// ended. The gate is per call, so unrelated calls never wait on each other.
/// </para>
/// </summary>
internal sealed class TrackedCall(
    ActiveCallKey key,
    ICall call,
    CallLog log,
    EventHandler<CallStateChangedEventArgs> handler) : IDisposable
{
    /// <summary>Workspace, channel and provider call id.</summary>
    public ActiveCallKey Key { get; } = key;

    /// <summary>Owning workspace, taken from the key.</summary>
    public string WorkspaceKey => Key.WorkspaceKey;

    /// <summary>The provider's call.</summary>
    public ICall Call { get; } = call;

    /// <summary>History record for this call.</summary>
    public CallLog Log { get; } = log;

    /// <summary>The state-changed handler, kept so it can be detached exactly once.</summary>
    public EventHandler<CallStateChangedEventArgs> Handler { get; } = handler;

    /// <summary>Serializes this call's transitions.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>
    /// How far the call has progressed. Advances only forward, so a duplicate or late callback
    /// for a stage already passed is ignored instead of rewriting history.
    /// </summary>
    public CallLifecycleStage Stage { get; private set; } = CallLifecycleStage.Started;

    /// <summary>
    /// Advances to <paramref name="stage"/> when it is ahead of the current one. Returns false
    /// for a repeated or out-of-order transition, which is the caller's signal to do nothing.
    /// </summary>
    public bool TryAdvanceTo(CallLifecycleStage stage)
    {
        if (stage <= Stage)
        {
            return false;
        }

        Stage = stage;
        return true;
    }

    /// <inheritdoc />
    /// <summary>
    /// The line this call claimed from its origin's quota, given back when the call is untracked.
    /// <see langword="null"/> when no quota applied.
    /// </summary>
    public IDisposable? QuotaReservation { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        // Released here rather than on the Terminated transition: untracking is the one path every
        // ending goes through, so a quota cannot drain through an ending nobody thought of.
        QuotaReservation?.Dispose();
        Gate.Dispose();
    }
}
