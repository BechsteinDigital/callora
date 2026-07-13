namespace Callora.Contracts.Communication;

/// <summary>
/// Outcome of one <see cref="IRecordingConsentCall.RequestRecordingConsentAsync"/> run.
/// </summary>
public enum RecordingConsentResult
{
    /// <summary>The remote party granted consent; recording may start.</summary>
    Granted = 0,

    /// <summary>The remote party denied consent; recording must not start.</summary>
    Denied = 1,

    /// <summary>No response within the configured window; treat as denied.</summary>
    Timeout = 2,

    /// <summary>The call ended before the consent interaction completed.</summary>
    CallEnded = 3
}
