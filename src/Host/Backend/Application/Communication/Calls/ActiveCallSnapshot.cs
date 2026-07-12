namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// Point-in-time view of one tracked call, serialized to API and SSE consumers.
/// </summary>
public sealed record ActiveCallSnapshot(
    string CallId,
    string WorkspaceKey,
    string ChannelId,
    string Direction,
    string State,
    string TargetValue,
    string? TargetDisplayName,
    DateTimeOffset StartedAtUtc);
