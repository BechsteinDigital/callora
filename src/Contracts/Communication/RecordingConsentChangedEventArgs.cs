namespace Callora.Contracts.Communication;

/// <summary>
/// Payload of <see cref="IRecordingConsentCall.ConsentChanged"/>.
/// </summary>
public sealed class RecordingConsentChangedEventArgs : EventArgs
{
    /// <summary>Creates the payload for one consent transition.</summary>
    public RecordingConsentChangedEventArgs(RecordingConsentState previousState, RecordingConsentState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    /// <summary>State before the transition.</summary>
    public RecordingConsentState PreviousState { get; }

    /// <summary>State after the transition.</summary>
    public RecordingConsentState CurrentState { get; }
}
