using Callora.Host.Backend.Domain.Jobs;

namespace Callora.Host.Backend.Application.Jobs;

/// <summary>
/// Persistence port for background jobs.
/// </summary>
public interface IBackgroundJobStore
{
    Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims the next runnable job and leases it for
    /// <paramref name="leaseDuration"/>. Runnable means a due pending job or a
    /// running job whose lease has expired (orphaned by a crashed worker), so
    /// this method also recovers stuck jobs. Returns null when nothing is
    /// runnable or the claim was lost to a competitor.
    /// </summary>
    Task<BackgroundJob?> TryClaimNextDueAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists state changes of one previously claimed job. Returns false when
    /// the lease was lost meanwhile (another worker reclaimed the job); the
    /// caller must then drop the result instead of overwriting the new owner.
    /// </summary>
    Task<bool> SaveAsync(BackgroundJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fails running jobs whose lease expired after the attempt budget was
    /// exhausted, so a job that repeatedly crashes its worker is not reclaimed
    /// forever. Returns the number of jobs failed.
    /// </summary>
    Task<int> FailExpiredExhaustedAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a pending job, or a running job with a still-valid lease, of
    /// the given type exists. A running job with an expired lease is treated as
    /// orphaned and does not count as active, so recurring scheduling is not
    /// blocked forever by a crashed run.
    /// </summary>
    Task<bool> HasActiveJobAsync(string jobType, DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recently created jobs.
    /// </summary>
    Task<IReadOnlyList<BackgroundJob>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes succeeded and failed jobs completed before the cutoff and
    /// returns the number of removed rows (retention, PLAT-240).
    /// </summary>
    Task<int> DeleteCompletedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default);
}
