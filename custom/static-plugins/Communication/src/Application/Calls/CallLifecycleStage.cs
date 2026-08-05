namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// The stages a tracked call passes through, in order (#113). Comparing them is what lets the
/// service ignore a duplicate or reordered provider callback: a transition is applied only when
/// it moves the call forward.
/// <para>
/// Deliberately coarser than <c>CallState</c>. This models the stages that change persisted
/// history, not every protocol state the provider reports.
/// </para>
/// </summary>
internal enum CallLifecycleStage
{
    /// <summary>Tracked and logged; ringing or connecting.</summary>
    Started = 0,

    /// <summary>Answered; talk time is running.</summary>
    Connected = 1,

    /// <summary>Finalized with a terminal outcome. No further transition is possible.</summary>
    Terminated = 2
}
