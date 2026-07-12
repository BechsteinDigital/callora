namespace Callora.Contracts.Communication;

/// <summary>
/// Channel-neutral lifecycle state of one call.
/// </summary>
public enum CallState
{
    /// <summary>The call is being established but has not reached the remote party yet.</summary>
    Connecting = 0,

    /// <summary>The remote party is being alerted.</summary>
    Ringing = 1,

    /// <summary>Media is flowing between both parties.</summary>
    Connected = 2,

    /// <summary>The call has ended and will not change state again.</summary>
    Terminated = 3,
}
