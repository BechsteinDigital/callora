namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Read-only view of one live call, returned to callers of <see cref="ICallControlService"/> so they
/// never handle the underlying <see cref="ICall"/> directly.
/// </summary>
/// <param name="CallId">Stable call identifier.</param>
/// <param name="Direction">Call direction relative to the platform.</param>
/// <param name="State">Current lifecycle state.</param>
/// <param name="Target">Remote participant address.</param>
public sealed record CallSnapshot(
    string CallId,
    CallDirection Direction,
    CallState State,
    string Target);
