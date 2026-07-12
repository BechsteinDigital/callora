namespace Callora.Host.Backend.Domain.Jobs;

/// <summary>
/// One durable background job with retry bookkeeping.
/// </summary>
public sealed class BackgroundJob
{
    private BackgroundJob()
    {
    }

    public Guid Id { get; private set; }

    public string JobType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public string? WorkspaceKey { get; private set; }

    public BackgroundJobStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public int MaxAttempts { get; private set; }

    public DateTimeOffset ScheduledAtUtc { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static BackgroundJob Create(
        string jobType,
        string payloadJson,
        DateTimeOffset scheduledAtUtc,
        int maxAttempts,
        string? workspaceKey,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        ArgumentNullException.ThrowIfNull(payloadJson);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        return new BackgroundJob
        {
            Id = Guid.NewGuid(),
            JobType = jobType.Trim(),
            PayloadJson = payloadJson,
            WorkspaceKey = string.IsNullOrWhiteSpace(workspaceKey) ? null : workspaceKey.Trim(),
            Status = BackgroundJobStatus.Pending,
            AttemptCount = 0,
            MaxAttempts = maxAttempts,
            ScheduledAtUtc = scheduledAtUtc,
            CreatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Marks one attempt as started.
    /// </summary>
    public void MarkRunning(DateTimeOffset nowUtc)
    {
        Status = BackgroundJobStatus.Running;
        AttemptCount++;
        StartedAtUtc = nowUtc;
    }

    /// <summary>
    /// Marks the job as successfully completed.
    /// </summary>
    public void MarkSucceeded(DateTimeOffset nowUtc)
    {
        Status = BackgroundJobStatus.Succeeded;
        CompletedAtUtc = nowUtc;
        LastError = null;
    }

    /// <summary>
    /// Records one failed attempt: reschedules with the given delay while
    /// attempts remain, otherwise transitions to <see cref="BackgroundJobStatus.Failed"/>.
    /// </summary>
    public void MarkFailedAttempt(string error, TimeSpan retryDelay, DateTimeOffset nowUtc)
    {
        LastError = error;

        if (AttemptCount >= MaxAttempts)
        {
            Status = BackgroundJobStatus.Failed;
            CompletedAtUtc = nowUtc;
            return;
        }

        Status = BackgroundJobStatus.Pending;
        ScheduledAtUtc = nowUtc + retryDelay;
    }
}
