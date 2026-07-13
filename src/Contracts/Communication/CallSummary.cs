namespace Callora.Contracts.Communication;

/// <summary>
/// Point-in-time view of one tracked call, exchanged between communication
/// plugins and platform surfaces (API, SSE, flows). Field names are part of
/// the public API shape consumed by the shells.
/// </summary>
/// <param name="CallId">Stable call identifier.</param>
/// <param name="WorkspaceKey">Owning workspace.</param>
/// <param name="ChannelId">Channel the call runs on.</param>
/// <param name="Direction">Call direction as string (Inbound/Outbound).</param>
/// <param name="State">Lifecycle state as string.</param>
/// <param name="TargetValue">Remote party address, e.g. phone number.</param>
/// <param name="TargetDisplayName">Optional remote party display name.</param>
/// <param name="StartedAtUtc">Tracking start time.</param>
public sealed record CallSummary(
    string CallId,
    string WorkspaceKey,
    string ChannelId,
    string Direction,
    string State,
    string TargetValue,
    string? TargetDisplayName,
    DateTimeOffset StartedAtUtc);
