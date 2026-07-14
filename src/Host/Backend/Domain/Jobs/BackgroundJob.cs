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

    /// <summary>
    /// When the current running lease expires. While set and in the future the
    /// job is owned by a worker; once elapsed a competing worker may reclaim the
    /// job as orphaned (crash recovery). Null when the job is not running.
    /// </summary>
    public DateTimeOffset? LeaseExpiresAtUtc { get; private set; }

    /// <summary>
    /// Fencing token rotated on every claim. Persisted as a concurrency token, so
    /// a worker whose lease was reclaimed by another worker can no longer save the
    /// job — its update matches no row and is rejected (no split-brain writes).
    /// </summary>
    public Guid LeaseToken { get; private set; }

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
    /// Marks one attempt as started and leases the job for the given duration.
    /// The lease bounds how long this attempt may run before another worker may
    /// reclaim the job as orphaned; counting the attempt here (not on completion)
    /// keeps a poison job that crashes the worker from being reclaimed forever.
    /// </summary>
    public void MarkRunning(DateTimeOffset nowUtc, TimeSpan leaseDuration)
    {
        Status = BackgroundJobStatus.Running;
        AttemptCount++;
        StartedAtUtc = nowUtc;
        LeaseExpiresAtUtc = nowUtc + leaseDuration;
        LeaseToken = Guid.NewGuid();
    }

    /// <summary>
    /// Marks the job as successfully completed and releases the lease.
    /// </summary>
    public void MarkSucceeded(DateTimeOffset nowUtc)
    {
        Status = BackgroundJobStatus.Succeeded;
        CompletedAtUtc = nowUtc;
        LeaseExpiresAtUtc = null;
        LastError = null;
    }

    /// <summary>
    /// Records one failed attempt and releases the lease: reschedules with the
    /// given delay while attempts remain, otherwise transitions to
    /// <see cref="BackgroundJobStatus.Failed"/>.
    /// </summary>
    public void MarkFailedAttempt(string error, TimeSpan retryDelay, DateTimeOffset nowUtc)
    {
        LastError = error;
        LeaseExpiresAtUtc = null;

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
