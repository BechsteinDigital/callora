using Callora.Core.Application.Jobs;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Domain.Jobs;

namespace Callora.Core.Application.Jobs;

/// <summary>
/// Claims and executes one due background job per call, including retry
/// bookkeeping with exponential backoff.
/// </summary>
public sealed class BackgroundJobProcessor(
    IBackgroundJobStore jobStore,
    BackgroundJobHandlerResolver handlerResolver,
    BackgroundJobOptions options,
    ILogger<BackgroundJobProcessor> logger,
    // Optional: Ein Host ohne Fehlerbudget rechnet nichts zu und verhält sich unverändert.
    Callora.Core.Application.Plugins.PluginFaultRegistry? faults = null,
    Callora.Core.Application.Plugins.IPluginAvailabilityEvaluator? availability = null)
{
    /// <summary>
    /// Processes the next due job. Returns false when no job was due.
    /// </summary>
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        await jobStore.FailExpiredExhaustedAsync(nowUtc, cancellationToken).ConfigureAwait(false);

        var job = await jobStore
            .TryClaimNextDueAsync(nowUtc, options.LeaseDuration, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return false;
        }

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

        // A revoked entitlement darkens a plugin's HTTP routes (REV2 §13) but used to
        // leave its queue running: webhooks delivered, mail sent, data synced for a
        // workspace that no longer holds the plugin. The gate belongs here too — and it
        // parks rather than fails, so a billing outage cannot burn the retry budget.
        if (availability is not null &&
            handlerResolver.ResolveOwner(job.JobType) is { } owningPlugin)
        {
            // Ein Job ohne Workspace ist plattformweite Arbeit, und die Frage dazu lautet
            // anders: nicht "darf dieses Plugin in Workspace W arbeiten", sondern "darf es
            // auf diesem Host überhaupt arbeiten". Beides ist beantwortbar, also wird auch
            // beides gefragt.
            var verdict = string.IsNullOrWhiteSpace(job.WorkspaceKey)
                ? await availability.EvaluatePlatformAsync(owningPlugin, cancellationToken).ConfigureAwait(false)
                : await availability.EvaluateAsync(owningPlugin, job.WorkspaceKey, cancellationToken).ConfigureAwait(false);
            if (!verdict.IsAvailable)
            {
                job.Defer(
                    string.IsNullOrWhiteSpace(job.WorkspaceKey)
                        ? $"Plugin '{owningPlugin}' is not available on this host."
                        : $"Plugin '{owningPlugin}' is not available in workspace '{job.WorkspaceKey}'.",
                    options.UnavailableRetryDelay,
                    DateTimeOffset.UtcNow);
                await jobStore.SaveAsync(job, cancellationToken).ConfigureAwait(false);
                logger.LogInformation(
                    "Job {JobId} ({JobType}) parked: plugin {PluginId} is unavailable in scope {WorkspaceKey} ({UnmetFactors}).",
                    job.Id,
                    job.JobType,
                    owningPlugin,
                    job.WorkspaceKey ?? "<platform>",
                    string.Join(", ", verdict.UnmetFactors));
                return true;
            }
        }

        try
        {
            var context = new BackgroundJobExecutionContext(
                job.Id,
                job.JobType,
                job.PayloadJson,
                job.WorkspaceKey,
                job.AttemptCount);

            // Der Handler läuft unter der Herkunft seines Exports, damit alles, was er
            // auslöst — Datenbankkommandos vor allem — ihm zugerechnet werden kann. Ein
            // Host-Handler hat keinen Eigentümer und läuft ohne Markierung; sonst trüge
            // Host-Arbeit den Namen des Plugins, das zufällig davor lief.
            var owner = handlerResolver.ResolveOwner(job.JobType);
            using (owner is null ? null : Diagnostics.PluginExecutionScope.Enter(owner))
            {
                await handler.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            }

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

            // Hintergrundarbeit ist der Fall, den sonst niemand meldet: Vor einer fehlgeschlagenen
            // Anfrage sitzt jemand, der sich beschwert; vor einem gescheiterten Job sitzt niemand.
            // Genau deshalb gehört er ins Budget.
            if (faults is not null && handlerResolver.ResolveOwner(job.JobType) is { } owner)
            {
                faults.Record(owner, Plugins.PluginFaultOrigin.Job);
            }
        }

        if (!await jobStore.SaveAsync(job, cancellationToken).ConfigureAwait(false))
        {
            logger.LogWarning(
                "Job {JobId} ({JobType}) lost its lease before saving; another worker owns it now.",
                job.Id,
                job.JobType);
            return true;
        }

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
