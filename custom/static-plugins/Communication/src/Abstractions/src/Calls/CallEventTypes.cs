namespace Callora.Plugin.Communication.Abstractions;

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

    // Recording-consent events lived here without anything ever raising them, because recording
    // itself is not implemented (#116). A published event name is a promise consumers can match on;
    // they come back with the feature that produces them.
}
