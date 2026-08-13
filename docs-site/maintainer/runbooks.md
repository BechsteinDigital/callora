# Runbooks

Operational procedures for running Callora in production. Monitoring assets live
under `docs/monitoring/`; the source runbooks under `docs/portal/runbooks/`.

## Plugin-lifecycle SLO and alerting

Source: `docs/monitoring/PLUGIN_LIFECYCLE_SLO.md`.

### SLOs (15-minute rolling window)

| SLO | Target |
|---|---|
| Activation latency (p95) | `<= 750 ms` |
| Lifecycle error rate | `<= 2%` |
| Stability success rate | `>= 99.5%` |
| Minimum sample to evaluate | `200` lifecycle operations |

### Metrics

- Counter `callora.plugin.lifecycle.operations` → Prometheus
  `callora_plugin_lifecycle_operations_total`.
- Histogram `callora.plugin.lifecycle.duration.ms` → Prometheus
  `callora_plugin_lifecycle_duration_ms_bucket`.
- Labels: `plugin_lifecycle_action`, `plugin_lifecycle_outcome`.

Telemetry is exported via OTLP; set `Observability__OtlpEndpoint` (empty disables
it).

### Dashboards and alerts

- **Grafana:** import `docs/monitoring/grafana/callora-plugin-lifecycle-slo-dashboard.json`.
- **Prometheus alerts:** `docs/monitoring/prometheus/plugin-lifecycle-slo-alerts.yml`,
  group `callora-plugin-lifecycle-slo`, all `for: 10m`, service
  `callora-host-backend`:

  | Alert | Severity | Fires when |
  |---|---|---|
  | `CalloraPluginActivationLatencySloBreached` | warning | p95 activate duration `> 750 ms` over 15m |
  | `CalloraPluginLifecycleErrorRateSloBreached` | critical | failure rate over all operations `> 0.02` |
  | `CalloraPluginLifecycleStabilitySloBreached` | critical | success rate `< 0.995` |

### When an SLO alert fires

1. Confirm the sample size is above the `200`-operation floor — below it, the
   signal is noise.
2. Check `plugin_lifecycle_action` / `plugin_lifecycle_outcome` breakdown to
   isolate the offending plugin and operation (install vs activate vs deactivate).
3. If a single plugin is failing to activate, treat it as a bad build: deactivate
   it via the API and, if it is a known-bad artifact, revoke its content hash
   (`BackendHost__RevokedContentHashes`) so it cannot reload. See
   [Migration & Rollback](migration-and-rollback.md).

## Workspace template rollout

Source: `docs/portal/runbooks/workspace-template-rollout.md`.

**Preconditions:** the target template version is registered and active; the
entitlement is set for the target workspace(s); the operator has the
`extension.update` permission.

**Rollout (narrowest scope first for canaries):**

1. Register / update the template definition.
2. Set a **system default** only for a broad baseline.
3. Prefer a **tenant-level default** for tenant-scoped changes.
4. Use a **workspace-level override** for a canary.
5. Verify the effective template:
   - `GET /api/workspace-templates/workspaces/{workspaceKey}/effective`
   - `GET /workspace/templates/effective`

### Workspace template rollback

Roll back via the scoped rollback endpoints (POST), re-checking the effective
response after each step:

```text
POST /api/workspace-templates/workspaces/{workspaceKey}/{templateKey}/rollback
POST /api/workspace-templates/tenants/{tenantKey}/{templateKey}/rollback
POST /api/workspace-templates/system/{templateKey}/rollback
```

**Incident triage — distinguish the failure level:**

- **Definition-level** (the template version itself is broken): deactivate the bad
  version via the activation endpoint.
- **Assignment-level** (the wrong template is assigned somewhere): roll back the
  **narrowest scope first** — workspace → tenant → system.

Capture request/response evidence throughout. Post-incident: add a regression test
and, if a contract ambiguity caused it, update the plugin authoring guidance.

## Incident basics

### Job queue stuck

Background jobs (lifecycle winddown, the `host.retention.cleanup` retention sweep,
plugin work) run on the host job queue.

1. Check whether the app is healthy: `GET /health` (liveness) and `GET /ready`
   (readiness — verifies the database). A failing `/ready` points at the DB.
2. Verify PostgreSQL connectivity and that startup migrations completed — startup
   takes a `pg_advisory_lock` while migrating; a stuck migration blocks readiness
   for every instance. Check the DB for a held advisory lock if startup hangs.
3. Inspect the audit log / lifecycle metrics for the last successful and last
   failed operation to locate the stall.

### Plugin unload / activation failure

1. Read the failure from the lifecycle metrics and the audit trail (every
   install-gate decision is audited).
2. Common causes, in order: **signature/trust** (unsigned in production, untrusted
   signer, or a revoked fingerprint/hash), **contract incompatibility**, **missing
   tenant entitlement**, **failed compliance metadata**. All four are hard
   activation gates.
3. Deactivate the plugin through the API to stop its data flow. Because the DB is
   the source of truth, the deactivation persists across restarts.
4. To permanently block a bad build, add its content hash to
   `BackendHost__RevokedContentHashes`; runtime rehydration enforces revocation at
   load, so it will not come back.

### Surface render failure / degradation

Public surfaces are SSR via `Callora.Surface.Rendering` (Nunjucks on the hardened
Jint sandbox).

1. A single template failing to render is expected to be **contained** by the
   sandbox (timeout / memory / recursion limits) — it should not take the host
   down. Confirm the failure is isolated to one surface/template.
2. If a template is hitting sandbox limits (timeouts), treat it as a bad template:
   roll the surface back to the previous known-good template bundle via the
   workspace-template rollback path above.
3. If rendering degrades broadly (not one template), check host health/readiness
   and CPU/memory — the sandbox limits are per-render, so broad degradation points
   at the host, not one template.

### Surface render metrics

Meter `Callora.Surface.Rendering`, picked up by the `Callora.*` wildcard in the
host composition — no extra registration.

| Metric | Type | What it answers |
|---|---|---|
| `callora.surface.render.requests` | Counter | How many renders, split by outcome and failure reason |
| `callora.surface.render.duration.ms` | Histogram | How long a render takes |

Both carry the same four dimensions: `workspace.key`, `surface.key`,
`surface.render.outcome` (`success`/`failure`) and `surface.render.reason`.

The reason is one of a fixed set, never free text — that is what makes it safe to
group on:

| Reason | Meaning |
|---|---|
| `none` | Success. Set so the tag schema is identical on both outcomes |
| `route_not_found` | No surface for this host and path. `workspace.key` is empty |
| `visibility_denied` | The surface exists, this visitor may not see it (answered 404, not 403) |
| `sign_in_required` | The surface wants a sign-in and the visitor has none |
| `access_rejected` | The surface's access gate refused |
| `data_missing` | A required data contributor does not know this address (404) |
| `data_unavailable` | A required data contributor could not answer (503) |
| `path_not_claimed` | The sub-path belongs to no surface that claims it |

**When reading these, mind two things.** Requests to platform-owned paths
(`/api/…` and friends) are *not* counted at all — the catch-all answers them with
404 before the measurement starts, so a mistyped API call cannot inflate the
surface error rate. And `sign_in_required` is an outcome, not an incident: exclude
it before alerting on the failure ratio, or every login prompt reads as a fault.

Traces come from the `Callora.Surface.Rendering` activity source: one
`surface.render` span per request with a child span per resolution step
(`resolve-route`, `resolve-theme`, `resolve-ui-chain`, `establish-caller`). That
is where you find out *which* step spent the time — deliberately not a metric
dimension, because per-step timings per surface would multiply the time series.

**Suggested alerts** (no SLO agreed yet — pick thresholds against your own
baseline):

- Failure ratio excluding `sign_in_required`, over a 15-minute window
- p99 of `callora.surface.render.duration.ms` per workspace
- Any sustained `data_unavailable` — a required contributor is down, and the
  surface is degraded for everyone on it
