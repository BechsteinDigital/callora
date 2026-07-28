namespace Callora.Plugin.Communication.Domain.Calls;

/// <summary>Terminal (or in-flight) outcome of a call, recorded in the call history.</summary>
public enum CallOutcome
{
    /// <summary>The call is still in progress (not yet finalized).</summary>
    InProgress = 0,

    /// <summary>Answered and completed normally.</summary>
    Completed = 1,

    /// <summary>Inbound call that was never answered.</summary>
    Missed = 2,

    /// <summary>Explicitly rejected.</summary>
    Rejected = 3,

    /// <summary>Ended due to an error.</summary>
    Failed = 4,

    /// <summary>Remote party was busy.</summary>
    Busy = 5,

    /// <summary>The call rang but was never answered (no pickup / timed out).</summary>
    NoAnswer = 6,

    /// <summary>The invitation was cancelled before it was answered.</summary>
    Canceled = 7
}
