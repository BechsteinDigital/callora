namespace Callora.Plugin.Communication.Abstractions;

/// <summary>How a DTMF collection ended.</summary>
public enum DtmfEntryOutcome
{
    /// <summary>The caller entered a complete entry — by reaching the expected length or by submitting.</summary>
    Completed,

    /// <summary>The caller cleared what they had typed, handing the decision back to the consumer.</summary>
    Cleared,

    /// <summary>The caller stopped typing for longer than the allowed pause.</summary>
    TimedOut,

    /// <summary>The call ended while collecting. Not an error — a caller who hangs up is an ordinary ending.</summary>
    CallEnded,

    /// <summary>
    /// The collection was replaced by a newer one on the same call, or cancelled by its caller. Also
    /// not an error: something else took over, and this entry simply no longer has an answer.
    /// </summary>
    Superseded,
}
