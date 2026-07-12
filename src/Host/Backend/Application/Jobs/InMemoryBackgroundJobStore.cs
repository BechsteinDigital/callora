using Callora.Host.Backend.Application.Abstractions.Jobs;
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

    public Task<BackgroundJob?> TryClaimNextDueAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            var job = _jobs.Values
                .Where(x => x.Status == BackgroundJobStatus.Pending && x.ScheduledAtUtc <= nowUtc)
                .OrderBy(x => x.ScheduledAtUtc)
                .ThenBy(x => x.CreatedAtUtc)
                .FirstOrDefault();

            if (job is null)
                return Task.FromResult<BackgroundJob?>(null);

            job.MarkRunning(nowUtc);
            return Task.FromResult<BackgroundJob?>(job);
        }
    }

    public Task SaveAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_syncLock)
        {
            _jobs[job.Id] = job;
        }

        return Task.CompletedTask;
    }

    public Task<bool> HasActiveJobAsync(string jobType, CancellationToken cancellationToken = default)
    {
        lock (_syncLock)
        {
            var hasActive = _jobs.Values.Any(x =>
                string.Equals(x.JobType, jobType, StringComparison.OrdinalIgnoreCase) &&
                x.Status is BackgroundJobStatus.Pending or BackgroundJobStatus.Running);
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
}
