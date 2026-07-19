# Repository Structure

Callora is the **framework** repository: a set of packable .NET 10 libraries plus
first-party plugins and the documentation site. The runnable entrypoint and
package composition live in the separate `callora-production` repository. The
solution that ties everything together is `Callora.Host.sln`.

## Top-level layout

```
src/                 framework libraries (the platform)
custom/              first-party plugins + the plugin SDK
tests/               xUnit test projects (Core + analyzers) and test plugins
docfx/               this documentation site
docs/                local design notes, ADRs, runbooks (gitignored in part)
scripts/             dev helpers (dev-build.sh, dev-test.sh, dev-check.sh, build-repo-map.sh)
ops/                 local frontdoor config, plans, specs
apps/                LEGACY Nuxt shells (workspace-shell + legacy-admin-shell)
Callora.Host.sln     the solution
Directory.Build.props / Directory.Packages.props   shared build + Central Package Management
global.json          pinned .NET 10 SDK
```

> **Note:** `apps/` is **legacy**. The admin shell now lives colocated in
> `src/Administration` and the surface runtime in `src/Surface.Rendering`. The
> `apps/workspace-shell` and `apps/legacy-admin-shell` still build in CI and are
> deployed separately, but new work does not go there.

## `src/` — the framework libraries

| Project | Package / purpose |
|---|---|
| `src/Core` | `Callora.Core` — domain-neutral platform core: Identity/RBAC, tenancy, plugin lifecycle, the event bus, persistence, the operator API host surface. This is the library the distribution publishes as the host backend. AGPL-3.0. |
| `src/Administration` | `Callora.Administration` — operator API plus the colocated Vue 3 admin shell (`Resources/app/administration`). Ships its built SPA as a static web asset served at `/admin`. AGPL-3.0. |
| `src/Workspace` | `Callora.Workspace` — the workspace (surface) API. To be renamed `Callora.Surface`. |
| `src/Surface.Rendering` | `Callora.Surface.Rendering` — server-side surface template rendering (Nunjucks on a hardened Jint sandbox, ADR-015) plus the colocated Vue surface runtime (`Resources/app/surface`) served at `/surface-app`. |
| `src/Host/Cli` | `Callora.Host.Cli` — the `callora` CLI (plugin contract-test kit, `plugin sign`). |
| `src/Analyzers` | `Callora.Analyzers` — Roslyn governance analyzers (CAL0001 internal-consumption guard, CAL0003 contract-documentation, extension-point-id analyzer). Referenced as an analyzer by the framework projects. |

Each framework library carries `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
for public-surface baseline tracking (any change to the public API shows up as a
reviewable diff).

## `custom/` — plugins and the SDK

| Path | Purpose |
|---|---|
| `custom/static-plugins/Communication` | System-tier VoIP/communication plugin (`Callora.Plugin.Communication`), bundled with the distribution. Ships its public contracts in `Communication/Abstractions` (`Callora.Plugin.Communication.Abstractions`). |
| `custom/plugins/Dialer` | Dynamically installable Dialer plugin (`Callora.Plugins.Dialer`). |
| `custom/plugins/*`, `custom/static-plugins/*` | Additional first-party plugins (e.g. TemplateAlpha). |
| `custom/surface-sdk` | `@callora/surface-sdk` — the plugin surface SDK. Apache-2.0. |

Plugins carry their **own EF Core migrations** and live in an isolated
`plugin_<id>` PostgreSQL schema (e.g. `plugin_communication`). See
[Migration & Rollback](migration-and-rollback.md).

## `tests/`

| Project | Scope |
|---|---|
| `tests/Callora.Core.Tests` | Host/core behaviour, plus architecture tests that enforce the structure rules (no nested/partial types, one type per file, DDD layering). |
| `tests/Callora.Analyzers.Tests` | Roslyn analyzer verification. |
| `tests/TestPlugins/*` | Plugin fixtures (e.g. `Callora.TestPlugin.Exporting`) used by the lifecycle tests. |

Fast tests run without external services (in-memory stores). Slow tests are tagged
`[Trait("Category","Slow")]` and use Testcontainers PostgreSQL — see
[Build & Release](build-and-release.md).

## `docfx/`

The conceptual documentation site (this guide included) is built with **VitePress**
under `docs-site/`. The **.NET API reference** is generated separately by DocFX
(`docfx/docfx.json`, from the XML docs of `src/**/*.csproj` and
`custom/plugins/**/*.csproj`, excluding tests and `bin`/`obj`) and served at `/api/`.

## Module boundaries and dependency direction

The dependency direction is strict and enforced by analyzers and architecture
tests. It is the single most important structural invariant a maintainer protects.

- **`Core` never references the modules.** `Administration`, `Workspace`, and
  `Surface.Rendering` all reference `Core`; `Core` references none of them.
  Identity/RBAC and tenancy stay in `Core`.
- **Within every project**, the DDD layering from `CODE_STRUCTURE_RULES.md` holds:
  `Domain` depends on nothing (no EF, no ASP.NET); `Application` depends only on
  `Domain` and defines ports as interfaces; `Infrastructure` implements those
  ports; `Api` stays thin and delegates to `Application`. Wiring (port → adapter)
  happens only in the composition root.
- **The API top level splits `Workspace/`** (tenant-scoped) **from `Admin/`**
  (operator-scoped).
- **Plugins build against the contract, not against internals.** Framework
  assemblies set `CalloraFrameworkAssembly=true` in their `.csproj` and may
  consume the `[CalloraInternal]` surface. Any other compilation — every plugin —
  leaves it `false`, and the CAL0001 analyzer rejects consumption of the internal
  surface. The contract surface (public contracts, `Extensibility`,
  `[CalloraExtensible]`) is the defined extension boundary.

> **Status:** `docs/REPO_MAP.md` is auto-generated and, at time of writing, still
> reflects an older layout (`src/Host`, `src/Contracts`, `src/Abstractions`). The
> live filesystem and this guide are authoritative; regenerate the map with
> `scripts/build-repo-map.sh` when it drifts.
