using Callora.Host.Backend.Application.Jobs;
using Callora.Host.Backend.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL-backed background job store. Claims are made atomic via a
/// conditional UPDATE on the pending status, so competing workers cannot
/// run the same job twice.
/// </summary>
public sealed class EfBackgroundJobStore(HostPersistenceDbContext dbContext) : IBackgroundJobStore
{
    public async Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<BackgroundJob?> TryClaimNextDueAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        var candidateId = await dbContext.BackgroundJobs
            .AsNoTracking()
            .Where(x =>
                (x.Status == BackgroundJobStatus.Pending && x.ScheduledAtUtc <= nowUtc) ||
                (x.Status == BackgroundJobStatus.Running && x.LeaseExpiresAtUtc != null && x.LeaseExpiresAtUtc < nowUtc && x.AttemptCount < x.MaxAttempts))
            .OrderBy(x => x.ScheduledAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidateId is null)
            return null;

        var leaseExpiresAtUtc = nowUtc + leaseDuration;
        var leaseToken = Guid.NewGuid();
        var claimed = await dbContext.BackgroundJobs
            .Where(x => x.Id == candidateId &&
                (x.Status == BackgroundJobStatus.Pending ||
                 (x.Status == BackgroundJobStatus.Running && x.LeaseExpiresAtUtc != null && x.LeaseExpiresAtUtc < nowUtc && x.AttemptCount < x.MaxAttempts)))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, BackgroundJobStatus.Running)
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                    .SetProperty(x => x.StartedAtUtc, nowUtc)
                    .SetProperty(x => x.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(x => x.LeaseToken, leaseToken),
                cancellationToken)
            .ConfigureAwait(false);

        if (claimed == 0)
            return null;

        return await dbContext.BackgroundJobs
            .SingleAsync(x => x.Id == candidateId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> SaveAsync(BackgroundJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // The lease was reclaimed by another worker (fencing token changed);
            // this worker's result must not overwrite the new owner.
            return false;
        }
    }

    public Task<int> FailExpiredExhaustedAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        return dbContext.BackgroundJobs
            .Where(x =>
                x.Status == BackgroundJobStatus.Running &&
                x.LeaseExpiresAtUtc != null &&
                x.LeaseExpiresAtUtc < nowUtc &&
                x.AttemptCount >= x.MaxAttempts)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, BackgroundJobStatus.Failed)
                    .SetProperty(x => x.CompletedAtUtc, nowUtc)
                    .SetProperty(x => x.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(x => x.LastError, "Lease expired after exhausting the attempt budget."),
                cancellationToken);
    }

    public Task<bool> HasActiveJobAsync(string jobType, DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobType))
            return Task.FromResult(false);

        var normalizedJobType = jobType.Trim();
        return dbContext.BackgroundJobs
            .AsNoTracking()
            .AnyAsync(
                x => x.JobType == normalizedJobType &&
                     (x.Status == BackgroundJobStatus.Pending ||
                      (x.Status == BackgroundJobStatus.Running && x.LeaseExpiresAtUtc != null && x.LeaseExpiresAtUtc >= nowUtc)),
                cancellationToken);
    }

    public async Task<IReadOnlyList<BackgroundJob>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BackgroundJobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<int> DeleteCompletedBeforeAsync(
        DateTimeOffset cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        return dbContext.BackgroundJobs
            .Where(x =>
                (x.Status == BackgroundJobStatus.Succeeded || x.Status == BackgroundJobStatus.Failed) &&
                x.CompletedAtUtc != null &&
                x.CompletedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
