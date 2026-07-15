namespace Callora.Core.Application.Jobs;

/// <summary>
/// Options for the background job worker and recurring scheduler.
/// </summary>
public sealed class BackgroundJobOptions
{
    /// <summary>Idle delay between worker polls when no job is due.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Interval between recurring-job scheduler evaluations.</summary>
    public TimeSpan SchedulerInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Base delay for exponential retry backoff (doubles per attempt).</summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a claimed job stays leased before a competing worker may reclaim
    /// it as orphaned. Must exceed the longest expected job runtime; a too-short
    /// value risks a second worker reclaiming a job that is still running.
    /// </summary>
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Maximum number of jobs returned by the monitoring endpoint.</summary>
    public int RecentListLimit { get; set; } = 100;
}
