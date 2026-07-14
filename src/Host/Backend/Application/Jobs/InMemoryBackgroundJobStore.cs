using Callora.Host.Backend.Application.Jobs;
using Callora.Host.Backend.Domain.Jobs;

namespace Callora.Host.Backend.Application.Jobs;

/// <summary>
/// Thread-safe in-memory job store for tests and hosts without database.
/// </summary>
public sealed class InMemoryBackgroundJobStore : IBackgroundJobStore
{
    private readonly object _syncLock = new();
    private readonly Dictionary<Guid, BackgroundJob> _jobs = [];

    public Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_syncLock)
        {
            _jobs[job.Id] = job;
        }

        return Task.CompletedTask;
    }

    public Task<BackgroundJob?> TryClaimNextDueAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            var job = _jobs.Values
                .Where(x =>
                    (x.Status == BackgroundJobStatus.Pending && x.ScheduledAtUtc <= nowUtc) ||
                    (x.Status == BackgroundJobStatus.Running && x.LeaseExpiresAtUtc is not null && x.LeaseExpiresAtUtc < nowUtc && x.AttemptCount < x.MaxAttempts))
                .OrderBy(x => x.ScheduledAtUtc)
                .ThenBy(x => x.CreatedAtUtc)
                .FirstOrDefault();

            if (job is null)
                return Task.FromResult<BackgroundJob?>(null);

            job.MarkRunning(nowUtc, leaseDuration);
            return Task.FromResult<BackgroundJob?>(job);
        }
    }

    public Task<bool> SaveAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_syncLock)
        {
            // Single-process store: the tracked job is the only instance, so
            // there is no lost-lease race — the save always applies.
            _jobs[job.Id] = job;
        }

        return Task.FromResult(true);
    }

    public Task<int> FailExpiredExhaustedAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            var exhausted = _jobs.Values
                .Where(x =>
                    x.Status == BackgroundJobStatus.Running &&
                    x.LeaseExpiresAtUtc is not null &&
                    x.LeaseExpiresAtUtc < nowUtc &&
                    x.AttemptCount >= x.MaxAttempts)
                .ToArray();

            foreach (var job in exhausted)
            {
                job.MarkFailedAttempt("Lease expired after exhausting the attempt budget.", TimeSpan.Zero, nowUtc);
            }

            return Task.FromResult(exhausted.Length);
        }
    }

    public Task<bool> HasActiveJobAsync(string jobType, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            var hasActive = _jobs.Values.Any(x =>
                string.Equals(x.JobType, jobType, StringComparison.OrdinalIgnoreCase) &&
                (x.Status == BackgroundJobStatus.Pending ||
                 (x.Status == BackgroundJobStatus.Running && x.LeaseExpiresAtUtc is not null && x.LeaseExpiresAtUtc >= nowUtc)));
            return Task.FromResult(hasActive);
        }
    }

    public Task<IReadOnlyList<BackgroundJob>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            IReadOnlyList<BackgroundJob> jobs = _jobs.Values
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(limit)
                .ToArray();
            return Task.FromResult(jobs);
        }
    }

    public Task<int> DeleteCompletedBeforeAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            var expiredIds = _jobs.Values
                .Where(x =>
                    x.Status is BackgroundJobStatus.Succeeded or BackgroundJobStatus.Failed &&
                    x.CompletedAtUtc is not null &&
                    x.CompletedAtUtc < cutoffUtc)
                .Select(x => x.Id)
                .ToArray();

            foreach (var id in expiredIds)
            {
                _jobs.Remove(id);
            }

            return Task.FromResult(expiredIds.Length);
        }
    }
}
