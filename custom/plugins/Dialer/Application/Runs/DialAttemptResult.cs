namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Result of one dial attempt within a run.
/// </summary>
public sealed record DialAttemptResult(
    string Number,
    DialAttemptOutcome Outcome,
    string? ErrorMessage,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset EndedAtUtc);
