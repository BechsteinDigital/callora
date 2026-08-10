# Operations

This page covers the day-2 concerns of running Callora: watching background
jobs, wiring business events out over webhooks, monitoring against SLOs, and the
rate limits that protect the API.

## Background jobs

Deferred and recurring work — flow execution, webhook delivery, entitlement sync
— runs as durable **background jobs**. The Jobs screen (`/jobs`) is a read-only
view of the queue (API: `GET /api/jobs`, `limit` up to 100). Operators see all
jobs; a workspace-scoped caller sees only their workspace's jobs.

Each job reports its type, status, workspace, attempt count and max attempts,
schedule/created/started/completed timestamps, and the last error. A job is in
one of four states:

| State | Meaning |
|---|---|
| `Pending` | Scheduled, or waiting to retry after a failed attempt |
| `Running` | Currently executing, holding an active lease |
| `Succeeded` | Completed successfully |
| `Failed` | Retries exhausted |

### Leases and the reaper

A running job holds a time-bounded **lease** (default five minutes) with a
rotating fencing token. The lease prevents double execution: if a worker crashes
mid-job, the lease expires and another worker can reclaim the job, while the old
worker — whose token is now stale — cannot write back a stale result. The
**reaper** recovers stuck work: a job whose lease has expired *and* whose
attempts are exhausted is marked `Failed` rather than being reclaimed forever, so
a poison job cannot loop indefinitely.

### Retries

There is no manual retry button. Failed attempts are rescheduled automatically
with exponential backoff (base 30s, doubling) until `maxAttempts` is reached;
after that the job is `Failed`. Different job types set their own attempt limits
(for example webhook delivery allows five attempts).

## Webhooks

Webhooks push platform **business events** to your HTTP endpoints. Manage
subscriptions under `/webhooks` (API base `/api/webhooks`):

| Action | Endpoint |
|---|---|
| List | `GET /api/webhooks` |
| Create | `POST /api/webhooks` |
| Enable / disable | `PUT /api/webhooks/{id}/activation?isActive=...` |
| Delete | `DELETE /api/webhooks/{id}` |

A subscription names an **event** (dotted names, `*` wildcard supported), a
**target URL** (absolute HTTP/HTTPS), a **secret** (write-only; never echoed),
an optional **workspace key**, and an `includeSensitiveData` flag. Reading
subscriptions needs `webhook.read`; managing them needs `webhook.manage`.

### Delivery

When a matching business event fires, Callora enqueues a durable delivery job and
POSTs a JSON envelope:

```json
{
  "event": "<eventName>",
  "workspaceKey": "<workspaceKey or null>",
  "occurredAtUtc": "<ISO-8601>",
  "data": { }
}
```

Each request carries `X-Callora-Event` (the event name) and `X-Callora-Signature`
(an HMAC-SHA256 of the body, keyed by your secret, as `sha256=<hex>`) — verify
the signature on your side to authenticate the call. By default payloads are
minimized with sensitive fields redacted; set `includeSensitiveData: true` to opt
into full payloads.

### Events you can subscribe to

Host business events currently include:

| Event | Fires when |
|---|---|
| `user.created` / `user.updated` / `user.deleted` | An account is created, updated, or deleted |
| `workspace.created` / `workspace.updated` / `workspace.deleted` | A workspace changes |
| `workspace.member-assigned` / `workspace.member-removed` | Membership changes |
| `media.uploaded` / `media.deleted` | A media asset is added or removed |

Plugins publish their own events too — a telephony plugin contributes its `call.*`
events, for example. Which ones exist depends on what is installed; the catalogue
under `GET /api/events/catalog` always shows the live set.

## Monitoring and SLOs

Callora emits OpenTelemetry metrics and ships a reference monitoring setup for
the plugin lifecycle. The definitions live in
`docs/monitoring/PLUGIN_LIFECYCLE_SLO.md`,
with a Grafana dashboard and Prometheus alert rules under
`docs/monitoring/grafana/` and `docs/monitoring/prometheus/`.

The plugin-lifecycle SLOs (15-minute rolling window, evaluated once at least
~200 operations are observed):

| SLO | Target |
|---|---|
| Activation latency (p95) | ≤ 750 ms |
| Lifecycle error rate | ≤ 2% |
| Lifecycle success rate (stability) | ≥ 99.5% |

They are backed by two metrics — a lifecycle operations counter
(`callora_plugin_lifecycle_operations_total`) and a duration histogram
(`callora_plugin_lifecycle_duration_ms`) — labeled by action and outcome. The
Prometheus rules alert on a p95 breach (warning) and on error-rate or
stability breaches (critical). Point your own Grafana/Prometheus at the shipped
configs to reproduce the dashboard and alerts.

## Rate limiting

The host applies fixed-window rate limits to protect the API, partitioned per
client (by `X-Forwarded-For` first address, else remote IP):

| Policy | Default limit | Applies to |
|---|---|---|
| `auth` | 5 requests/minute | Login endpoints |
| `api` | 600 requests/minute | General API |

Either can be tuned via `BackendHost__RateLimitAuthPerMinute` and
`BackendHost__RateLimitApiPerMinute` (set to 0 to disable). Over the limit the
API returns `429 Too Many Requests` with a `Retry-After: 60` header; requests are
rejected immediately rather than queued.
