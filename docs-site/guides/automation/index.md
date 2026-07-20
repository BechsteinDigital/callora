# Automation

A Callora plugin does more than answer HTTP requests. It runs work **on its own schedule**,
**reacts to platform events**, lets operators **compose low-code rules and flows**, and
**pushes events to external systems**. This section is the map of those four automation
surfaces — what each one is, when to reach for it, and how to build it.

All four share one delivery backbone: the **durable job queue**, which runs work with
**at-least-once** delivery, leases, crash recovery, and a bounded retry budget. Understanding
that backbone once ([Background jobs](./background-jobs)) pays off across every page here.

## What you'll learn

- The four ways a plugin automates work, and the one-line difference between them
- A decision table for picking the right tool for a given task
- How automation (asynchronous) differs from a **business-event listener** (synchronous reaction)
- A suggested learning path through this section

## The four automation surfaces

| Surface | Contract | What it does | Page |
| --- | --- | --- | --- |
| **Background jobs** | `IBackgroundJobHandler` | Run async work once, off the request thread, with retries and crash recovery | [Background jobs](./background-jobs) |
| **Recurring jobs** | `IRecurringJobProvider` | Run work on a fixed interval (nightly cleanup, periodic sync) | [Recurring jobs](./recurring-jobs) |
| **Rules & flows** | `IRuleConditionEvaluator` + `IFlowActionHandler` | Contribute *conditions* and *actions* that operators compose into low-code automation | [Rules](./rules), [Flows](./flows) |
| **Webhooks** | `IWebhookEventPublisher` + `WebhookSubscription` | Push platform events to external systems as signed HTTP POSTs | [Webhooks](./webhooks) |

Each contract is a Callora **extension point**: you `implement` it and `context.Export<T>(...)`
your instance from your plugin's `StartAsync` — see
[Exporting extensions](/guides/fundamentals/exporting-extensions). The host discovers the
export and wires it in.

## When to use each

| You want to… | Use | Why |
| --- | --- | --- |
| Do slow/external work without blocking an HTTP response | **Background job** | Async, durable, retried; survives a restart |
| Run the same work every night / every 15 minutes | **Recurring job** | The scheduler enqueues a job for you on the interval |
| Let an operator say "*when a call rings from an unknown number, reject it*" | **Rule + flow** | You ship the `call.direction` condition and `call.reject` action; the operator wires them |
| Notify a CRM/Slack/webhook endpoint that "*a call ended*" | **Webhook** | Delivered as a signed, retried HTTP POST outside the platform |
| React **synchronously** to an event, possibly to **veto** it before it commits | **Business-event listener** | In-process, ordered, cancelable — see below |

## Automation vs. business-event listeners

There are two families of "react to something happening," and mixing them up is the most common
early mistake.

A **business-event listener** (`IBusinessEventListener`) is **synchronous and in-process**: the
publisher calls every listener in priority order and *waits* for them, and a listener of a
`MutableBusinessEvent` can call `Cancel()` to veto the operation **before it commits**. Use it
when you must influence the outcome *now* — validate, enrich, or block. It is covered in
[Backend extensions](/guides/backend-extensions#business-event-listeners) and
[Events & jobs](/guides/events-and-jobs).

Everything in *this* section is **asynchronous**: the work runs later, on a worker thread,
durably. A job cannot veto the operation that scheduled it — it has already happened.

::: info The two families connect
Flows and webhooks are *driven by* the business-event bus. When any business event fires,
`FlowBusinessEventListener` matches active flows and enqueues a `flow.execute` **job**; the
webhook dispatcher matches subscriptions and enqueues a `webhook.deliver` **job**. So an
event (synchronous) fans out into automation (asynchronous). You publish the event; the host
does the fan-out.
:::

## Learning path

1. **[Background jobs](./background-jobs)** — the foundation. Every other page builds on the
   queue's at-least-once + idempotency contract.
2. **[Recurring jobs](./recurring-jobs)** — schedule a job on an interval.
3. **[Rules](./rules)** — contribute a condition operators can test in a flow.
4. **[Flows](./flows)** — contribute an action, and see how operators compose it via `/api/flows`.
5. **[Webhooks](./webhooks)** — emit events to the outside world with signing and data minimization.

## Next steps

- [Background jobs](./background-jobs) — start here
- [Events & jobs](/guides/events-and-jobs) — the conceptual overview of the bus and the queue
- [Backend extensions](/guides/backend-extensions) — synchronous listeners, services, controllers
- [Architecture](/concepts/architecture) — where these subsystems sit in the platform
- [REST API reference](/reference/rest-api) — the operator endpoints
