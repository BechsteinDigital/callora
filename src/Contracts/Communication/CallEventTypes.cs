namespace Callora.Contracts.Communication;

/// <summary>
/// Stable event type codes on the live call stream. Part of the public
/// contract: flows, webhooks and SSE consumers match on these values.
/// </summary>
public static class CallEventTypes
{
    /// <summary>Inbound call is ringing.</summary>
    public const string Ringing = "call.ringing";

    /// <summary>Outbound call was placed.</summary>
    public const string Placed = "call.placed";

    /// <summary>Call transitioned to another lifecycle state.</summary>
    public const string StateChanged = "call.state-changed";

    /// <summary>Call ended.</summary>
    public const string Ended = "call.ended";

    /// <summary>Recording consent was granted (PLAT-241).</summary>
    public const string ConsentGranted = "call.consent-granted";

    /// <summary>Recording consent was denied (PLAT-241).</summary>
    public const string ConsentDenied = "call.consent-denied";
}
