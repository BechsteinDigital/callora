namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Point-in-time view of one dial run.
/// </summary>
public sealed record DialRunSnapshot(
    string RunId,
    string WorkspaceKey,
    DialRunStatus Status,
    IReadOnlyList<DialAttemptResult> Attempts,
    string? ErrorMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);
