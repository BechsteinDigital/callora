# Maintainer Guide

This guide is for the people who **run, build, and release Callora** — platform
maintainers and deployment operators. If you are writing a plugin or using the
admin shell as an end user, this is not your section; see the
[API Reference](/api/) and the plugin SDK instead.

Callora is a domain-neutral .NET 10 plugin platform. The host is a pure platform
— authentication and RBAC, user and plugin management, a business-event bus, and
the surface-rendering layer — while everything domain-specific lives in plugins.
Distribution follows an **open-core** model: an AGPL-3.0 core and Apache-2.0 SDK.
The repository is private today; a public Community Edition is planned.

> **Status:** The repository is the **framework** — a set of packable libraries.
> The runnable process entrypoint and package composition live in the separate
> `callora-production` repository, which assembles these packages into a
> distribution (one app container + Postgres). That repository is referenced here
> but is out of scope for this guide.

## What a maintainer owns

- The **solution** (`Callora.Host.sln`), Central Package Management, and the
  frontend build targets that bundle the admin shell and surface runtime.
- The **CI/CD workflows** — build, test, DocFX, and tagged releases.
- **Deployment** of the self-contained app plus its PostgreSQL database.
- **Database lifecycle** — host and per-plugin EF Core migrations, the RBAC seed,
  and safe plugin install/activate/rollback.
- **Security posture** — the trust model, plugin signing and fingerprint trust,
  the hardened template sandbox, RBAC, CSRF, rate limiting, secrets, and the
  compliance baseline.
- **Operations** — SLOs, alerting, and incident runbooks.

## Sections

- **[Repository Structure](repository-structure.md)** — the `src` / `custom` /
  `tests` / `docfx` layout, module boundaries, and dependency direction.
- **[Build & Release](build-and-release.md)** — the solution, CPM, frontend build
  targets and skip flags, test tiers, CI workflows, versioning and packaging.
- **[Deployment](deployment.md)** — running the self-contained app on
  docker-compose or a VPS, Postgres, configuration hygiene, TLS, and where the
  admin and public surfaces are served.
- **[Migration & Rollback](migration-and-rollback.md)** — EF Core migrations
  (host and per-plugin), the DB-as-truth lifecycle model, the admin→superadmin
  seed, and safe rollback.
- **[Security](security.md)** — the trust model (ADR-013), plugin signing, the
  template sandbox, RBAC, CSRF and rate limiting, secrets, and the DSGVO /
  EU AI Act compliance baseline.
- **[Runbooks](runbooks.md)** — the plugin-lifecycle SLO and alerts, workspace
  template rollout, and incident basics.

## Ground rules

The engineering and structure rules that maintainers enforce on every change:

- `ENGINEERING_RULES.md` — DDD layering, no nested types, one type per file,
  tests for every functional change, no DONE claims without evidence.
- `CODE_STRUCTURE_RULES.md` — the mandatory project/folder structure mirrored by
  host and plugins.
- `docs/QUALITY_STANDARDS.md` — the concrete quality gates (warnings-as-errors,
  analyzers, coverage ratchet, API conventions).

Build the documentation locally with `dotnet tool restore` followed by
`dotnet docfx docfx/docfx.json --serve`.
