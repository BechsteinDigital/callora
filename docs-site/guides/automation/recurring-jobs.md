# Recurring jobs

A recurring job runs the same work on a **fixed interval** — a nightly cleanup, a periodic sync,
a heartbeat. You don't schedule each run yourself: you declare the *definition*, and the host's
scheduler enqueues a normal [background job](./background-jobs) whenever the interval elapses.

That means a recurring job is really two pieces: an **`IRecurringJobProvider`** that declares
*when and what*, plus an ordinary **`IBackgroundJobHandler`** that does the work. The provider
feeds the queue; the queue runs the handler. Everything you already know about jobs — leases,
retries, idempotency — still applies.

The worked reference is the host's own retention cleanup
(`src/Core/Application/Retention/RetentionRecurringJobProvider.cs`).

## What you'll learn

- How to declare a schedule with `IRecurringJobProvider` and `RecurringJobDefinition`
- How the host scheduler turns a definition into enqueued jobs
- The "no overlapping run" and "first run after one interval" guarantees
- A worked nightly-cleanup example: provider + handler

::: tip Prerequisites

- You've read [Background jobs](./background-jobs) — a recurring job enqueues one.
- A plugin with a `StartAsync(IHostPluginContext context, …)` entry point that can
  `context.Export<T>(...)` — see [Exporting extensions](/guides/fundamentals/exporting-extensions).
:::

## Declare a schedule

Implement `IRecurringJobProvider` (`src/Core/Application/Jobs/Contracts/`). It returns the
recurring jobs it owns:

```csharp
public interface IRecurringJobProvider
{
    IReadOnlyList<RecurringJobDefinition> GetDefinitions();
}
```

Each `RecurringJobDefinition` describes one fixed-interval job:

```csharp
public sealed record RecurringJobDefinition(
    string JobType,       // the handler's routing key
    string PayloadJson,   // JSON passed to each enqueued job
    TimeSpan Interval,    // fixed interval between enqueues
    int MaxAttempts = 1,  // attempts per enqueued job
    string? WorkspaceKey = null); // optional workspace; null = host-wide
```

The host's retention provider shows the whole shape — including returning **no** definitions when
the feature is turned off (a provider can decide its own schedule dynamically):

```csharp
public sealed class RetentionRecurringJobProvider(RetentionOptions options) : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions()
    {
        if (!options.Enabled)
        {
            return []; // nothing scheduled while retention is disabled
        }

        return
        [
            new RecurringJobDefinition(
                RetentionCleanupJobHandler.JobTypeName,
                PayloadJson: "{}",
                Interval: options.SweepInterval)
        ];
    }
}
```

## How the host runs it

The `RecurringJobEnqueuer` (`src/Core/Application/Jobs/`) evaluates every provider (host
providers *and* plugin exports) on a timer — the `BackgroundJobOptions.SchedulerInterval`
(default **5 seconds**). Each cycle, for every due definition, it enqueues one ordinary
`BackgroundJob`. From that point on the work is a normal job: claimed under a lease, executed by
the handler for its `JobType`, retried on failure per `MaxAttempts`.

Two guarantees shape the timing:

- **First run after one interval, not at boot.** The first time the enqueuer sees a definition it
  only records the start time and enqueues nothing — this avoids a "boot storm" of every recurring
  job firing at startup. The first actual run happens one `Interval` later.
- **No overlapping runs.** Before enqueuing, the enqueuer checks
  `HasActiveJobAsync(JobType, …)` and **skips the cycle** if a job of the same `JobType` is still
  `Pending` or `Running`. A slow run that takes longer than the interval will not pile up behind
  itself.

::: warning `Interval` is a floor, not a cron expression
Scheduling is **interval-based**, evaluated every `SchedulerInterval`. It is *not* wall-clock cron:
there is no "run at 02:00" — there is "run roughly every N". An `Interval` shorter than
`SchedulerInterval` still only fires once per scheduler tick. Definitions with a non-positive
`Interval` are ignored entirely.

> **Status:** For time-of-day precision (e.g. *nightly at 02:00*), gate the work inside the
> handler using the current time, or model it as a flow with a `time.window` condition — see
> [Rules](./rules). A calendar-cron schedule is not part of `RecurringJobDefinition` today.
:::

::: info Choosing `MaxAttempts` for a recurring job
`RecurringJobDefinition.MaxAttempts` defaults to **1**. For idempotent periodic work, `1` is
usually right: if a run fails, the *next interval* is another chance, so you rarely need per-run
retries. Raise it only when a single run's transient failure genuinely warrants an immediate retry.
:::

## Worked example: a nightly cleanup

Two types: a provider that schedules the sweep, and the handler that does it. The handler is an
ordinary `IBackgroundJobHandler` — the same contract as any [background job](./background-jobs),
so the idempotency rule applies (a sweep may run more than once).

**1. The provider** declares a daily interval:

```csharp
using Callora.Core.Application.Jobs.Contracts;

public sealed class StaleExportCleanupProvider : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() =>
    [
        new RecurringJobDefinition(
            JobType: StaleExportCleanupHandler.JobTypeName, // "exports.cleanup"
            PayloadJson: "{}",                              // no per-run parameters
            Interval: TimeSpan.FromDays(1),
            MaxAttempts: 1)
    ];
}
```

**2. The handler** deletes exports older than a threshold — written to be safe if it runs twice:

```csharp
using Callora.Core.Application.Jobs.Contracts;

public sealed class StaleExportCleanupHandler(
    IExportStore exports,
    TimeProvider clock,
    ILogger<StaleExportCleanupHandler> logger) : IBackgroundJobHandler
{
    public const string JobTypeName = "exports.cleanup";

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - TimeSpan.FromDays(30);

        // Deleting already-deleted rows is a no-op, so a second run is harmless — idempotent.
        var removed = await exports.DeleteOlderThanAsync(cutoff, cancellationToken);

        logger.LogInformation("Nightly export cleanup removed {Count} stale exports.", removed);
        // A throw here fails this run; the next interval retries the sweep.
    }
}
```

**3. Export both** from `StartAsync`:

```csharp
public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
{
    var exports = context.Services.GetRequiredService<IExportStore>();
    var clock = context.Services.GetRequiredService<TimeProvider>();
    var loggerFactory = context.Services.GetRequiredService<ILoggerFactory>();

    context.Export<IRecurringJobProvider>(new StaleExportCleanupProvider());
    context.Export<IBackgroundJobHandler>(new StaleExportCleanupHandler(
        exports, clock, loggerFactory.CreateLogger<StaleExportCleanupHandler>()));

    return ValueTask.CompletedTask;
}
```

That's the whole loop: the enqueuer sees your provider on its next cycle, waits one day, then
enqueues an `exports.cleanup` job; the queue claims it and runs your handler; the "no overlapping
runs" guard prevents a slow sweep from stacking. You can watch each run land in
[`GET /api/jobs`](./background-jobs#monitoring).

## Next steps

- [Background jobs](./background-jobs) — the handler contract and the idempotency rule
- [Rules](./rules) — a `time.window` condition for time-of-day gating
- [Retention & GDPR](/guides/data/retention-and-gdpr) — the host's own retention sweep
- [Exporting extensions](/guides/fundamentals/exporting-extensions) — exporting your provider
