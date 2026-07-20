# Deployment

Callora ships as a **self-contained app**: one application container (the composed
host built from these framework packages) plus a **PostgreSQL** database. It runs
on docker-compose or a single VPS. This page covers the operational shape;
the composition itself (image build, package assembly) lives in the separate
`callora-production` repository.

> **Status:** This repository provides the framework libraries and the dev/prod-like
> compose files below. The production composition (`callora-production`), the
> production container image, and the production NuGet feed are designed but live
> outside this repo. Treat the prod-like compose file as the reference shape, not
> a shipped production artifact.

## Topology

A single origin per workspace domain routes by path (the
`docs/CALLORA_PLATFORM_BETRIEBSMODELL.md` model), fronted by a reverse proxy
(Caddy in the local stacks):

| Path | Target |
|---|---|
| `/admin/*` | Admin UI (the colocated Vue shell, static web asset from `Callora.Administration`) |
| `/api/*` | Operator / control-plane API (plugin lifecycle, entitlements, policies, audit) |
| `/workspace/*` | Workspace API |
| `/surface-app/*` | Surface runtime (static web asset from `Callora.Surface.Rendering`) |
| `/*` | Public workspace surfaces (SSR) |

DNS for all workspace domains points at the same frontdoor IP; the workspace is
resolved from `Host + Path`.

## Compose stacks in this repo

| File | Brings up | Use |
|---|---|---|
| `docker-compose.yml` | `callora-backend` (SDK image, `dotnet watch` on `src/Core`, port `5000`) + `postgres:16` (port `5432`). Two services. | Local dev, hot reload. |
| `docker-compose.frontdoor.yml` | Overlay adding a `caddy:2.8` frontdoor on port `8080` over the dev stack (single-origin path routing). | Layer with `-f docker-compose.yml -f docker-compose.frontdoor.yml`. |
| `docker-compose.prod-like.yml` | Built self-contained images: `callora-backend` (from `src/Core/Dockerfile`), `callora-admin-shell`, `callora-workspace-shell`, a `caddy:2.8` frontdoor on `8080`, and `postgres:16`. Five services, no source mount, no `dotnet watch`. | Rehearse the production shape locally. |

Start the dev stack:

```bash
cp .env.example .env    # edit at least the DB connection + API key
set -a; source .env; set +a
docker compose -f docker-compose.yml up -d
curl http://localhost:5000/health
```

With the frontdoor, the app is reachable at `http://localhost:8080`.

## PostgreSQL

- One PostgreSQL database backs the whole app. The host schema and every plugin's
  `plugin_<id>` schema live on the **same database**.
- Migrations are applied automatically at startup under a **Postgres advisory
  lock**, so multiple app instances can start safely against one database. See
  [Migration & Rollback](migration-and-rollback.md).
- Default local credentials (from `.env.example`): database `callora_host`, user
  `callora`. **Change these for anything beyond local dev.**

Connection string (config key `BackendHost:DatabaseConnectionString`, env
`BackendHost__DatabaseConnectionString`):

```text
Host=postgres;Port=5432;Database=callora_host;Username=callora;Password=<secret>
```

## Configuration hygiene

ASP.NET configuration keys map from environment variables with double-underscore
(`BackendHost__DatabaseConnectionString` → `BackendHost:DatabaseConnectionString`);
arrays use `__0`, `__1`, object arrays `__0__Field`. Template: `.env.example`;
local `.env` is gitignored.

Keys a deployment operator must review:

| Key | Meaning |
|---|---|
| `BackendHost__DatabaseConnectionString` | PostgreSQL connection. |
| `BackendHost__RequireApiKeyAuthentication` / `BackendHost__ApiKeys__0` | Control-plane API key auth (header `X-Callora-Api-Key`). **Rotate the default key** `callora-local-dev-key-change-me`. |
| `BackendHost__AllowUnsignedPlugins` | Dev sets `true`; **production must be `false`** so every plugin (including bundled system plugins) must be signed and trusted. |
| `BackendHost__TrustedSigners__*` | Trusted signer public keys (see [Security](security.md)). |
| `BackendHost__RevokedSignerFingerprints__*` / `BackendHost__RevokedContentHashes__*` | Revocation lists, enforced at install and at runtime rehydration. |
| `BackendHost__AdminShellBaseUrl` (`/admin/`), `WorkspaceShellBaseUrl` (`/`), `PluginAssetBaseUrl` (`/plugin-assets`) | Routing base URLs. |
| `CalloraHosting__AutoLoadPlugins`, `AutoActivateInstalledPlugins`, `PluginDirectory` | Plugin auto-load behaviour on startup. |
| `Observability__OtlpEndpoint` (a.k.a. `Observability:OtlpEndpoint`) | OTLP exporter target; empty = telemetry off. |
| `Retention__*` | Retention sweep configuration (see below). |

Hardening notes from `docs/QUALITY_STANDARDS.md`:

- The default `JwtSigningKey` **throws outside Development** — you must supply a
  real signing key in production.
- Secret config and webhook secrets are encrypted in the database (the
  data-protection keyring lives in the DB) and redacted in API responses; PII is
  masked in logs.
- Configure `AllowedHosts` and any CORS/forwarded-headers settings for the real
  deployment origin.

## TLS

Terminate TLS at the reverse proxy / frontdoor. The local stacks use Caddy on port
`8080` without TLS; in production, put the app behind a TLS-terminating proxy
(Caddy with automatic HTTPS, or your existing ingress).

> **Status:** The local frontdoor config (`ops/local-frontdoor/Caddyfile*`) is
> dev-only and does not terminate TLS. Production TLS and the hardened frontdoor
> config are an operations task in the `callora-production` composition, not
> shipped here.

## Health and readiness

- `GET /health` — liveness, no dependency check.
- `GET /ready` — readiness, checks the database.

Wire `/ready` into your orchestrator's readiness probe and `/health` into the
liveness probe.

## Admin and public surfaces

- The **admin shell** is served at `/admin` from the static web assets bundled in
  `Callora.Administration`. Plugin admin UIs are loaded at runtime by the
  micro-frontend loader (backend manifest → `/plugin-assets`), so installing and
  activating a plugin surfaces its UI on a browser refresh **without a restart**.
- **Public surfaces** are server-side rendered by `Callora.Surface.Rendering`
  (Nunjucks on a hardened Jint sandbox), with the surface runtime served at
  `/surface-app`. Untrusted surface templates run inside the sandbox — see
  [Security](security.md).
