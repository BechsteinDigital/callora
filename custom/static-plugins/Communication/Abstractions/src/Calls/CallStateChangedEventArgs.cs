namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Payload for one call state transition.
/// </summary>
public sealed class CallStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Creates the event payload for one state transition.
    /// </summary>
    public CallStateChangedEventArgs(CallState previousState, CallState currentState)
    {
        PreviousState = previousState;
        CurrentState = currentState;
    }

    /// <summary>State before the transition.</summary>
    public CallState PreviousState { get; }

    /// <summary>State after the transition.</summary>
    public CallState CurrentState { get; }
}
