# Events & Jobs

Two runtime systems let a plugin react to and schedule work: the **business-event bus**
(synchronous, in-process, cancelable) and the **job queue** (asynchronous, durable,
at-least-once with HA safety).

## The business-event bus

The bus is the primary way plugins react to platform activity. It is a synchronous
publish/subscribe fan-out over the same shared, in-process types every plugin references.

### Publishing

The publisher (a host subsystem or a plugin) raises an event through **`IBusinessEventBus`**
(`src/Core/Application/Events/Contracts/`):

```csharp
public interface IBusinessEventBus
{
    Task PublishAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default);
}
```

An event implements **`IBusinessEvent`** — a stable `EventName`, an optional `WorkspaceKey`
scope (`null` = platform-wide), and a flat `ToEventData()` projection for templates and
flows:

```csharp
public interface IBusinessEvent : IHostEvent
{
    string EventName { get; }        // stable dotted name, e.g. "call.ringing"
    string? WorkspaceKey { get; }    // null = platform-wide
    IReadOnlyDictionary<string, string> ToEventData();
}
```

### Subscribing

A plugin reacts by exporting an **`IBusinessEventListener`**
([Backend Extensions](backend-extensions.md#business-event-listeners)). The listener is
called for every published event and filters on `EventName` itself:

```csharp
public sealed class CallLifecycleListener : IBusinessEventListener
{
    public int Priority => 0;

    public Task OnBusinessEventAsync(IBusinessEvent e, CancellationToken ct = default)
    {
        if (e.EventName == "call.ringing") { /* … */ }
        return Task.CompletedTask;
    }
}
```

### Ordering and cancellation

`BusinessEventBus` (`src/Core/Application/Events/Business/`) collects host and plugin
listeners, orders them by **descending `Priority`** (higher runs first), and dispatches in
order. For events that derive from **`MutableBusinessEvent`** — the before-commit events —
the base `InterceptableEvent` gives listeners three levers:

- `State` — a mutable dictionary to pass data between listeners in one dispatch.
- `StopPropagation()` — stop the remaining listeners; the bus checks
  `IsPropagationStopped` after each and breaks early.
- `Cancel()` — veto the operation; the publisher inspects `IsCanceled` after `PublishAsync`
  and aborts.

A mutable, cancelable event is Callora's equivalent of a Symfony/Shopware "before" event:

```csharp
public sealed class BeforeCallDialEvent(string workspaceKey, string number)
    : MutableBusinessEvent("call.before-dial", workspaceKey)
{
    public string Number { get; } = number;
    public override IReadOnlyDictionary<string, string> ToEventData()
        => new Dictionary<string, string> { ["number"] = Number };
}

// A listener can veto:
public Task OnBusinessEventAsync(IBusinessEvent e, CancellationToken ct)
{
    if (e is BeforeCallDialEvent dial && IsBlocked(dial.Number))
        dial.Cancel();
    return Task.CompletedTask;
}
```

The bus is synchronous and in-process. For work that must survive a restart or run out of
band, use the job queue.

## The job queue

The job queue runs durable background work with **at-least-once** delivery and the safety
properties needed for horizontal scale: leases, a reaper, idempotency, a fencing token, and
a bounded retry budget.

### Enqueuing

Enqueue through **`IBackgroundJobQueue`** with a **`BackgroundJobRequest`**
(`src/Core/Application/Jobs/`):

```csharp
public sealed record BackgroundJobRequest(
    string JobType,
    string PayloadJson,
    DateTimeOffset? RunAtUtc = null,   // delay/schedule; null = due now
    int MaxAttempts = 3,               // retry budget (HA)
    string? WorkspaceKey = null);

var jobId = await jobQueue.EnqueueAsync(
    new BackgroundJobRequest("voip.export", payloadJson, MaxAttempts: 5), ct);
```

### Handling

A plugin implements **`IBackgroundJobHandler`** (`src/Core/Application/Jobs/Contracts/`) and
exports it. The handler declares the `JobType` it serves and does the work:

```csharp
public interface IBackgroundJobHandler
{
    string JobType { get; }
    Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default);
}

public sealed record BackgroundJobExecutionContext(
    Guid JobId, string PayloadJson, string? WorkspaceKey, int Attempt);
```

### Leases, reaper, and recovery

The `BackgroundJob` aggregate (`src/Core/Domain/Jobs/`) carries the durable state
(`Status`, `AttemptCount`, `MaxAttempts`, `ScheduledAtUtc`, `LeaseExpiresAtUtc`,
`LeaseToken`). The processor loop (`BackgroundJobProcessor`) runs each tick:

1. **Reap** — `FailExpiredExhaustedAsync(nowUtc)` fails poison jobs whose lease expired and
   whose attempts are exhausted.
2. **Claim** — `TryClaimNextDueAsync(nowUtc, leaseDuration)` atomically claims the next due
   job *or reclaims one whose lease has expired* (a crashed worker's job), marks it
   `Running`, sets a **new** `LeaseExpiresAtUtc`, and mints a **fresh `LeaseToken`**.
3. **Execute** — invokes the handler for the job's `JobType`.
4. **Complete** — on success marks `Succeeded`; on failure records the attempt with a
   backoff delay (or fails permanently once `MaxAttempts` is reached).

This is how a crashed or stalled job recovers: its lease simply expires and the next tick
reclaims it. No job is lost, and none is stuck forever.

### The fencing token

`LeaseToken` (a `Guid` minted on each claim) is the **fencing token**. It is configured as
an **EF concurrency token** (`BackgroundJobEntityTypeConfiguration`), so when a slow worker
whose lease was already reclaimed by another worker tries to save its result, the concurrency
check fails and its write is rejected. This prevents split-brain: two workers can never both
commit an outcome for the same lease.

### The idempotency contract

Because delivery is **at-least-once** and leases can be reclaimed, a handler **may run more
than once for the same job**. Your `ExecuteAsync` must therefore be **idempotent**: guard
side effects with a natural key or an idempotency check keyed on `JobId` (or a payload key),
so a re-run produces no duplicate effect. This is the single most important rule for job
handlers — the queue guarantees the work runs, not that it runs exactly once.

### Monitoring

Recent jobs are readable (read-only) at `GET /api/jobs`.

## Choosing between them

| Use the… | when |
| --- | --- |
| **event bus** | you must react synchronously, in-process, and possibly veto an operation before it commits |
| **job queue** | the work is asynchronous, must survive a restart, or should retry with backoff |
