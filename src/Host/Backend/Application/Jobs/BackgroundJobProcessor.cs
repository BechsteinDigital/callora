using Callora.Host.Backend.Application.Abstractions.Jobs;
using Callora.Host.Backend.Domain.Jobs;
using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Host.Backend.Application.Jobs;

/// <summary>
/// Claims and executes one due background job per call, including retry
/// bookkeeping with exponential backoff.
/// </summary>
public sealed class BackgroundJobProcessor(
    IBackgroundJobStore jobStore,
    BackgroundJobHandlerResolver handlerResolver,
    BackgroundJobOptions options,
    ILogger<BackgroundJobProcessor> logger)
{
    /// <summary>
    /// Processes the next due job. Returns false when no job was due.
    /// </summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var job = await jobStore.TryClaimNextDueAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (job is null)
            return false;

        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        var handler = handlerResolver.Resolve(job.JobType);
        if (handler is null)
        {
            job.MarkFailedAttempt(
                $"No handler is registered for job type '{job.JobType}'.",
                RetryDelayFor(job),
                DateTimeOffset.UtcNow);
            await jobStore.SaveAsync(job, cancellationToken).ConfigureAwait(false);
            logger.LogWarning("No handler for job type {JobType} (job {JobId}).", job.JobType, job.Id);
            return true;
        }

        try
        {
            var context = new BackgroundJobExecutionContext(
                job.Id,
                job.JobType,
                job.PayloadJson,
                job.WorkspaceKey,
                job.AttemptCount);

            await handler.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            job.MarkSucceeded(DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.MarkFailedAttempt("Host shutdown interrupted the job.", TimeSpan.Zero, DateTimeOffset.UtcNow);
            await jobStore.SaveAsync(job, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            job.MarkFailedAttempt(ex.Message, RetryDelayFor(job), DateTimeOffset.UtcNow);
            logger.LogError(ex, "Job {JobId} ({JobType}) failed on attempt {Attempt}.", job.Id, job.JobType, job.AttemptCount);
        }

        await jobStore.SaveAsync(job, cancellationToken).ConfigureAwait(false);
        BackgroundJobTelemetry.RecordAttempt(
            job.JobType,
            job.Status.ToString(),
            System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return true;
    }

    private TimeSpan RetryDelayFor(BackgroundJob job)
    {
        var exponent = Math.Max(0, job.AttemptCount - 1);
        var factor = Math.Pow(2, Math.Min(exponent, 10));
        return TimeSpan.FromTicks((long)(options.RetryBaseDelay.Ticks * factor));
    }
}
