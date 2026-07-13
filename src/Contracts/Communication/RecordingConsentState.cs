namespace Callora.Contracts.Communication;

/// <summary>
/// Consent state for recording one call (§ 201 StGB / Art. 6 GDPR). Kept
/// separate from <see cref="CallState"/>: a call stays
/// <see cref="CallState.Connected"/> while consent is being requested.
/// </summary>
public enum RecordingConsentState
{
    /// <summary>No consent interaction has happened; recording is forbidden.</summary>
    NotRequested = 0,

    /// <summary>The consent announcement is playing / awaiting the response.</summary>
    Pending = 1,

    /// <summary>The remote party granted recording consent.</summary>
    Granted = 2,

    /// <summary>The remote party denied consent; recording is forbidden.</summary>
    Denied = 3
}
