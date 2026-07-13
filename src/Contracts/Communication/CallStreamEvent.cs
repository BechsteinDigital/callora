namespace Callora.Contracts.Communication;

/// <summary>
/// One event on the live call stream (types like "call.ringing",
/// "call.ended", "call.consent-granted").
/// </summary>
/// <param name="Type">Stable event type code.</param>
/// <param name="Call">Snapshot of the affected call.</param>
public sealed record CallStreamEvent(string Type, CallSummary Call);
