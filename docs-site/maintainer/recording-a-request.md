# Recording a request

Everything the host measures by default is an **aggregate**: job telemetry, lifecycle
telemetry, webhook telemetry, SLO evaluation. Those answer *is the platform healthy*.

None of them answers the question an operator actually asks when something is slow: **this
request took four seconds — who spent it?** Under
[ADR-013](https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-013-trust-model-trusted-in-process.md)
several plugins from different repositories share one process and one database connection,
so nothing about a query says who issued it. The recorder is the instrument for that
question, and it is the only one.

## It switches itself off

The recorder is **off by default** and **expires on its own**. There is deliberately no way
to enable it indefinitely.

That is not caution for its own sake. The failure mode of a diagnostic tool is not being
wrong — it is being switched on during an incident and forgotten, so that months later a
production host is still capturing every query of every request. A ceiling nobody can raise
is what makes it safe to turn on when it is needed.

| | |
| --- | --- |
| Default state | off |
| Maximum window | 10 minutes, whatever the caller asks for |
| Captured commands kept | 500, oldest dropped first |
| Permission | `diagnostics.record` |

A window longer than the ceiling is **clamped, not refused** — refusing would only teach
callers to ask for the maximum every time. The response tells you the window actually in
effect, which is what you need when planning a reproduction.

## Using it

```bash
# Record everything for two minutes
curl -X POST /api/diagnostics/recorder/start \
  -H 'Content-Type: application/json' \
  -d '{"windowSeconds": 120}'

# Or narrow it to one plugin — see below for why that matters
curl -X POST /api/diagnostics/recorder/start \
  -H 'Content-Type: application/json' \
  -d '{"windowSeconds": 120, "pluginId": "communication"}'

# Reproduce the slow request, then read what was captured
curl /api/diagnostics/recorder

# Stop early; what was captured stays readable
curl -X POST /api/diagnostics/recorder/stop
```

Each entry carries the plugin, the SQL, how long it took, and when:

```json
[
  {
    "pluginId": "communication",
    "commandText": "SELECT c.\"Id\", c.\"StartedAt\" FROM plugin_communication.\"Calls\" AS c",
    "durationMs": 412.7,
    "occurredAtUtc": "2026-08-24T11:04:22.118Z"
  }
]
```

::: tip Narrow to one plugin when you can
The ring holds 500 commands. On a busy host that is seconds, and the request you were
investigating has already been pushed out by the time you read it.
:::

## How attribution works

The host marks which plugin's code is running at the three points where it hands over
control — a plugin HTTP route, a background job whose handler a plugin owns, and a business
event delivered to a plugin listener. Those are the same three that enforce
[plugin availability](/reference/plugin-availability), so they already resolve the owning
plugin.

Two consequences worth knowing when reading a recording:

- **Host work shows `pluginId: null`** rather than inheriting whichever plugin ran before it.
  An attribution that guesses is worse than none: it names a culprit confidently and wrongly.
- **On HTTP the scope starts late** — after authentication, permission and availability
  checks. Those are the host's own work and are not on the plugin's bill.

## What it does not capture

- **Anything while switched off.** The cost then is a field read per command.
- **Timings other than database commands.** Request-level durations, CPU profiles and a
  business-event timeline are not part of this. The question it was built for is *which
  plugin*, and the database is where a shared process actually contends.

## Security

Attribution is **not writable by plugins**. `PluginExecutionScope` and the recorder are
marked `[CalloraInternal]`, so a plugin touching them trips
[`CAL0001`](/reference/analyzer-rules#cal0001-consuming-a-callorainternal-api-from-outside-the-framework)
at build time. Without that a plugin could file its own database work under a neighbour —
and it would do so precisely where the recorder is meant to be used: a support case, with a
customer waiting and several foreign plugins in one process.

Under ADR-013 the marker plus the analyzer is the available hardness. It makes forging
attribution a deliberate breach of a build-time rule rather than something reachable by
accident.

A recording contains **SQL command text**, including literals EF Core inlines into the
statement. That is a wider disclosure than any other monitoring endpoint makes, which is why
`diagnostics.record` is a permission of its own rather than part of `job.read`. Grant it to
the people who debug the platform, not to everyone who may watch it.
