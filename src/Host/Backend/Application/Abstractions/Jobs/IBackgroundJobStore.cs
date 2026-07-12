using Callora.Host.Backend.Domain.Jobs;

namespace Callora.Host.Backend.Application.Abstractions.Jobs;

/// <summary>
/// Persistence port for background jobs.
/// </summary>
public interface IBackgroundJobStore
{
    Task AddAsync(BackgroundJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims the next due pending job and marks it running.
    /// Returns null when no job is due or the claim was lost to a competitor.
    /// </summary>
    Task<BackgroundJob?> TryClaimNextDueAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists state changes of one previously claimed job.
    /// </summary>
    Task SaveAsync(BackgroundJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when a pending or running job of the given type exists.
    /// </summary>
    Task<bool> HasActiveJobAsync(string jobType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recently created jobs.
    /// </summary>
    Task<IReadOnlyList<BackgroundJob>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);
}
