namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Read-only view of one live call, returned to callers of <see cref="ICallControlService"/> so they
/// never handle the underlying <see cref="ICall"/> directly.
/// </summary>
/// <param name="CallId">Stable call identifier.</param>
/// <param name="Direction">Call direction relative to the platform.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="Target">Remote participant address.</param>
/// <param name="InboundIdentity">
/// Who called and which of our numbers they reached, on an inbound call. Null for an outbound one
/// and for a transport that reports nothing. Without it a panel shows a string of digits where the
/// network already said "Praxis Dr. Meier, weitergeleitet von der Zentrale".
/// </param>
public sealed record CallSnapshot(
    string CallId,
    CallDirection Direction,
    CallState State,
    string Target,
    InboundCallIdentity? InboundIdentity = null);
