namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Outcome of one dial attempt.
/// </summary>
public enum DialAttemptOutcome
{
    /// <summary>The remote party answered before the call ended.</summary>
    Connected = 0,

    /// <summary>The call ended without ever reaching connected state.</summary>
    NotConnected = 1,

    /// <summary>The call did not terminate within the configured timeout and was hung up.</summary>
    TimedOut = 2,

    /// <summary>Placing the call failed with an error.</summary>
    Failed = 3,
}
