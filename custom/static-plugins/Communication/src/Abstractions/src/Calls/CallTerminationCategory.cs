namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Protocol-neutral classification of why a call ended. Communication plugins map their
/// protocol-specific cause (e.g. a SIP status) onto one of these categories so consumers can
/// reason about call outcomes without knowing the transport.
/// </summary>
public enum CallTerminationCategory
{
    /// <summary>The call was answered and ended normally.</summary>
    Completed,

    /// <summary>The remote party was busy and could not take the call.</summary>
    Busy,

    /// <summary>The call rang but was never answered (timed out / no pickup).</summary>
    NoAnswer,

    /// <summary>The remote party actively declined the call.</summary>
    Rejected,

    /// <summary>The invitation was cancelled before it was answered.</summary>
    Canceled,

    /// <summary>The call ended because of an error (protocol/media/network failure).</summary>
    Failed,
}
