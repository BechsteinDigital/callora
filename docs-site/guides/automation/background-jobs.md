# Background jobs

A background job runs work **off the request thread**, **durably**, and **with retries**. You
enqueue a job with a JSON payload; the host stores it, a worker claims it later, and your
handler does the work. If the worker crashes mid-run, the job is recovered and retried — no
work is lost.

That durability comes with one contract you must honour: delivery is **at-least-once**, so your
handler **may run more than once for the same job**. It must be **idempotent**. This page shows
how to enqueue and handle a job, then works through each safety property — leases, the reaper,
the fencing token, the retry budget — and ends with a handler that is safely idempotent.

The worked reference throughout is a dialer plugin's dial run — a long-running job that
places calls from a list and must survive a host restart mid-run. It is the shape most
plugin work takes: triggered by an operator, slow, and worse than useless if it repeats an
effect on retry.

## What you'll learn

- How to enqueue work with `IBackgroundJobQueue.EnqueueAsync`
- How to implement and export an `IBackgroundJobHandler`
- The payload contract (`BackgroundJobExecutionContext`) and the result contract (throw = retry)
- How leases and the reaper recover a crashed job
- How the fencing token prevents split-brain writes
- How `MaxAttempts` bounds the retry budget with exponential backoff
- Why — and how — to make a handler idempotent
- How to monitor jobs via `GET /api/jobs`

::: tip Prerequisites

- A working plugin with a `StartAsync(IHostPluginContext context, …)` entry point — see
  [Plugin entry](/guides/fundamentals/plugin-entry).
- You can resolve host services from `context.Services` and `context.Export<T>(...)` your own —
  see [Exporting extensions](/guides/fundamentals/exporting-extensions).
:::

## Enqueue work

Resolve `IBackgroundJobQueue` from the host and call `EnqueueAsync` with a `BackgroundJobRequest`
(`src/Core/Application/Jobs/Contracts/`):

```csharp
public sealed record BackgroundJobRequest(
    string JobType,                    // handler routing key, e.g. "dialer.run"
    string PayloadJson,                // raw JSON passed to the handler
    DateTimeOffset? RunAtUtc = null,   // earliest run time; null = as soon as possible
    int MaxAttempts = 3,               // total attempts including the first
    string? WorkspaceKey = null);      // optional workspace scope
```

A coordinator enqueues one run like this:

```csharp
public sealed class DialRunCoordinator(
    DataStoreDialRunStore runStore,
    IBackgroundJobQueue jobQueue)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DialRunSnapshot?> StartRunAsync(
        string workspaceKey, DialRunOptions options, CancellationToken cancellationToken = default)
    {
        var snapshot = /* … create + persist the run snapshot … */;

        var payload = new DialRunJobPayload(
            snapshot.RunId, workspaceKey, (int)Math.Ceiling(options.CallTimeout.TotalSeconds));

        await jobQueue.EnqueueAsync(
            new BackgroundJobRequest(
                JobType: DialRunJobHandler.JobTypeName,          // "dialer.run"
                PayloadJson: JsonSerializer.Serialize(payload, JsonOptions),
                MaxAttempts: 1,
                WorkspaceKey: workspaceKey),
            cancellationToken);

        return snapshot;
    }
}
```

`EnqueueAsync` returns the new job's `Guid` id and returns immediately — the work has not run
yet. It runs when a worker next claims it.

::: info Keep the payload small and self-describing
The payload is opaque JSON. Prefer a small record with **identifiers** (a run id, a workspace
key) over embedding large blobs — the handler re-reads the current state from your store, which
also keeps the job correct if the underlying data changed between enqueue and execution.
:::

## Handle work

Implement `IBackgroundJobHandler` (`src/Core/Application/Jobs/Contracts/`). Declare the `JobType`
you serve and do the work in `ExecuteAsync`:

```csharp
public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default);
}
```

The context carries everything your handler needs (`BackgroundJobExecutionContext`):

```csharp
public sealed record BackgroundJobExecutionContext(
    Guid JobId,          // persistent job id — a stable idempotency key
    string JobType,      // routing key
    string PayloadJson,  // the JSON you enqueued
    string? WorkspaceKey,// the workspace scope you enqueued
    int Attempt);        // 1-based attempt number (2 = first retry)
```

The **result contract is exceptions**: return normally and the attempt is marked `Succeeded`;
**throw** and the attempt is marked failed and retried (until `MaxAttempts` is reached). There is
no "return false to retry" — success is the absence of a throw.

Export the handler from `StartAsync`:

```csharp
public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
{
    var jobQueue = context.Services.GetRequiredService<IBackgroundJobQueue>();
    // … build the handler's dependencies …
    context.Export<IBackgroundJobHandler>(new DialRunJobHandler(executor, numberStore, runStore));
    return ValueTask.CompletedTask;
}
```

The host resolves handlers by `JobType`; when a `dialer.run` job comes due, your handler runs.

::: warning No handler = failed attempt
If no handler is registered for a job's `JobType`, the processor records a failed attempt with
`"No handler is registered for job type '…'"` and retries on the backoff schedule. A job whose
handler you forgot to export will burn its whole attempt budget and land as a dead letter.
:::

## How the queue runs your job

The `BackgroundJobProcessor` loop (`src/Core/Application/Jobs/`) runs each tick:

1. **Reap** — `FailExpiredExhaustedAsync(nowUtc)` fails poison jobs whose lease has expired *and*
   whose attempts are exhausted.
2. **Claim** — `TryClaimNextDueAsync(nowUtc, leaseDuration)` atomically claims the next due job
   **or reclaims one whose lease expired** (a crashed worker's job), marks it `Running`, sets a
   fresh `LeaseExpiresAtUtc`, and mints a new `LeaseToken`.
3. **Execute** — invokes your handler for the job's `JobType`.
4. **Complete** — success → `Succeeded`; a throw → a failed attempt with a backoff delay, or a
   permanent `Failed` once `MaxAttempts` is reached.

The `BackgroundJob` aggregate (`src/Core/Domain/Jobs/`) carries the durable state:
`Status`, `AttemptCount`, `MaxAttempts`, `ScheduledAtUtc`, `LeaseExpiresAtUtc`, `LeaseToken`,
`LastError`. Its `Status` enum is `Pending → Running → Succeeded | Failed`.

### Leases & the reaper

When a worker claims a job it takes a **lease** — `LeaseExpiresAtUtc` is set to
`now + LeaseDuration` (default **5 minutes**, `BackgroundJobOptions.LeaseDuration`). While the
lease is live and in the future, the job belongs to that worker.

If the worker **crashes** mid-run, it never completes the job and never renews the lease. The
lease simply expires. On a later tick, `TryClaimNextDueAsync` sees an expired lease and
**reclaims** the job as orphaned — that is crash recovery, with no separate daemon required. The
**reaper** (`FailExpiredExhaustedAsync`) is the companion step: a poison job that keeps crashing
the worker eventually exhausts its attempts, and the reaper transitions it to `Failed` rather
than letting it be reclaimed forever.

::: warning Set `LeaseDuration` above your longest job
The lease must **exceed** the longest expected runtime of the job. If a job legitimately runs
longer than the lease, a second worker will reclaim it *while the first is still running* — and
now the job runs twice concurrently. This is exactly why the next two sections matter.
:::

### The fencing token

`LeaseToken` (a `Guid` minted on every claim) is the **fencing token**. It is configured as an
**EF concurrency token** (`BackgroundJobEntityTypeConfiguration`). So if a slow worker whose lease
was already reclaimed by another worker tries to save its result, the concurrency check matches
no row and the write is **rejected**:

```text
Job {JobId} ({JobType}) lost its lease before saving; another worker owns it now.
```

This prevents **split-brain**: two workers can never both commit an outcome for the same lease.
The fencing token protects the *database write*; it does **not** undo external side effects a
losing worker already performed — that is what idempotency is for.

### MaxAttempts & backoff

`MaxAttempts` (default **3**) is the total attempts *including the first*. On each failed attempt
the job reschedules with **exponential backoff**: `RetryBaseDelay` (default 30 s) doubled per
attempt (`RetryDelayFor`), capped at 2^10. When `AttemptCount` reaches `MaxAttempts`, the job
becomes `Failed` permanently and stops retrying — a dead letter, visible in `/api/jobs`.

Choose `MaxAttempts` by the nature of the work:

- **`1`** — the effect must happen at most as many times as the operator triggered it, and a
  retry would be wrong or is handled by the operator (dial runs and flow execution use `1`).
- **`3`–`5`** — transient-failure-prone work like network I/O; the webhook delivery job uses `5`.

### The idempotency contract

This is the single most important rule for job handlers.

Because delivery is **at-least-once** — a retry after failure, a reclaim after a crash, or a
long-running job reclaimed while still running — your `ExecuteAsync` **may run more than once
for the same job**. It must therefore be **idempotent**: running it twice must not double an
external effect (send, charge, provision, create).

Make it idempotent by guarding side effects with a **natural key** or a check keyed on a stable
identifier. Good keys, in order of preference:

- A **domain natural key** already in your data (an order id, a run id) — the strongest.
- `context.JobId` — stable across every retry/reclaim of the *same* job.
- A key inside your payload.

`context.Attempt` tells you *which* attempt this is (2 = first retry); use it for logging, not for
correctness.

## A complete, safely-idempotent handler

This handler charges an external provider exactly once per invoice, even if the queue runs it
several times. The idempotency guard is the invoice's own state plus a recorded provider
reference — not `Attempt`.

```csharp
using Callora.Core.Application.Jobs.Contracts;
using System.Text.Json;

public sealed record ChargeInvoicePayload(Guid InvoiceId);

public sealed class ChargeInvoiceJobHandler(
    IInvoiceStore invoices,
    IPaymentProvider payments,
    ILogger<ChargeInvoiceJobHandler> logger) : IBackgroundJobHandler
{
    public const string JobTypeName = "billing.charge-invoice";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<ChargeInvoicePayload>(context.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Charge payload could not be parsed.");

        var invoice = await invoices.GetAsync(payload.InvoiceId, cancellationToken)
            ?? throw new InvalidOperationException($"Invoice '{payload.InvoiceId}' was not found.");

        // Idempotency guard: if a previous attempt already charged, do nothing.
        if (invoice.IsPaid)
        {
            logger.LogInformation("Invoice {InvoiceId} already charged; skipping.", invoice.Id);
            return; // success — no duplicate charge
        }

        // Give the provider a deterministic idempotency key so even a mid-charge
        // crash (charged, but not yet saved) cannot double-charge on the retry.
        var idempotencyKey = $"invoice-{invoice.Id}";
        var reference = await payments.ChargeAsync(
            invoice.AmountCents, idempotencyKey, cancellationToken);

        invoice.MarkPaid(reference);
        await invoices.SaveAsync(invoice, cancellationToken);
        // Throwing anywhere above marks the attempt failed and schedules a retry.
    }
}
```

Two layers make this safe:

1. The `IsPaid` check skips work when a **prior attempt already succeeded and saved**.
2. The provider **idempotency key** covers the narrow window where a prior attempt charged but
   crashed before saving `IsPaid` — the provider deduplicates the second charge itself.

Together they satisfy the at-least-once contract: the queue guarantees the work *runs*, and your
handler guarantees the *effect* happens once.

## Monitoring

Recent jobs are readable (read-only) at:

```text
GET /api/jobs?limit=100
```

The endpoint (`src/Administration/Api/JobEndpoints.cs`) requires the **`job.read`** permission
and returns, per job: `Id`, `JobType`, `Status`, `WorkspaceKey`, `AttemptCount`, `MaxAttempts`,
`ScheduledAtUtc`, `CreatedAtUtc`, `StartedAtUtc`, `CompletedAtUtc`, and `LastError`.

Workspace-bound sessions see only their own workspace's jobs; operator sessions see all. A job
in `Failed` status with a non-null `LastError` is a **dead letter** — it exhausted its attempts,
and `LastError` holds the last exception message.

::: info There is no write endpoint
`/api/jobs` is read-only monitoring: no requeue, cancel, or delete over the API. To re-run failed
work, enqueue a fresh job (idempotently).
:::

## Next steps

- [Recurring jobs](./recurring-jobs) — enqueue this kind of job on a fixed interval
- [Flows](./flows) — flow execution is itself a `flow.execute` background job
- [Webhooks](./webhooks) — delivery runs as a `webhook.deliver` job with `MaxAttempts: 5`
- [Events & jobs](/guides/events-and-jobs) — the conceptual overview
- [Exporting extensions](/guides/fundamentals/exporting-extensions) — the export mechanism
- [REST API reference](/reference/rest-api) — `/api/jobs`
