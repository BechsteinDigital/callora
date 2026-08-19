# Build & Release

## Prerequisites

- **.NET 10 SDK**, pinned in `global.json` (`10.0.202`, `rollForward: latestFeature`).
  CI installs it via `actions/setup-dotnet` with `global-json-file: global.json`.
- **Node.js 22** — only needed when building the frontends (admin shell, surface
  runtime). Skip it with the flags below for a .NET-only build.

## Solution and Central Package Management

Everything builds through one solution, `Callora.Host.sln`. NuGet versions are
**centrally managed** in `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`);
individual `.csproj` files reference packages **without a version attribute**. To
add or bump a dependency, edit `Directory.Packages.props` — not the project files.

Shared build settings live in `Directory.Build.props`:

- `TreatWarningsAsErrors=true` — warnings fail the build. Every entry in the
  global `NoWarn` list carries a justification comment.
- `EnforceCodeStyleInBuild=true` — the `.editorconfig` style rules (braces,
  file-scoped namespaces, …) are build-enforced, not review-enforced.
- `GenerateDocumentationFile=true` (non-test projects) — feeds DocFX and lets the
  CAL0003 analyzer see `///` docs.
- `EnableNETAnalyzers=true`, `AnalysisLevel=10.0-recommended`.
- `MinVer` for versioning (see [Versioning](#versioning-and-releases)).

```bash
dotnet restore Callora.Host.sln
dotnet build Callora.Host.sln --configuration Release
dotnet test  Callora.Host.sln --configuration Release
```

## Frontend build targets and skip flags

The admin shell and surface runtime are colocated Vue apps built by **MSBuild
targets** during the .NET build, so `dotnet build` produces a complete artifact
with the SPAs bundled. Each target runs `npm ci` + `npm run build` (Vite) into the
project's `wwwroot`, then the Web SDK collects those as static web assets.

| Module | Frontend source | Output | Served at | Skip flag |
|---|---|---|---|---|
| `src/Administration` | `Resources/app/administration` | `wwwroot/admin` | `/admin` | `-p:SkipAdminFrontend=true` |
| `src/Surface.Rendering` | `Resources/app/surface` | `wwwroot/surface-app` | `/surface-app` | `-p:SkipSurfaceFrontend=true` |

The targets (`BuildAdminFrontend`, `BuildSurfaceFrontend`) run
`BeforeTargets="ResolveStaticWebAssetsInputs;BeforeBuild"` and key their
incrementality on the frontend sources, so a rebuild triggers only when those
change. The frontend **source** (TS/Vite/`node_modules`) is excluded from the
NuGet package and the static-web-asset set — only the built output rides along.

For a backend-only iteration (no Node installed):

```bash
dotnet build Callora.Host.sln -p:SkipAdminFrontend=true -p:SkipSurfaceFrontend=true
```

Build or test a single project directly, e.g. the surface layer without its SPA:

```bash
dotnet build src/Surface.Rendering/Callora.Surface.Rendering.csproj -p:SkipSurfaceFrontend=true
```

### One target at a time

A full run builds both Vue suites and every plugin cloned under `custom/`. When you are
working on one of them, `scripts/dev-build.sh` takes a target:

```bash
scripts/dev-build.sh --only admin        # the admin shell
scripts/dev-build.sh --only surface      # the surface runtime and SSR
scripts/dev-build.sh --only host         # the solution, both frontends off
scripts/dev-build.sh --plugins composer  # one plugin: C# plus its bundles
```

Roughly what that saves, measured on one machine: 31s for `host`, 15s for `surface`, 4s
for an incremental `admin`.

The targets are not a second way to build. `admin` and `surface` are a `dotnet build` on
that one project, and the frontend is produced by the project's own MSBuild target — the
script never states how a frontend is built, so there is nothing here that can drift from
the table above.

## Running tests

### Fast (default, no external services)

The main test suite runs entirely on in-memory stores and includes the
architecture tests (no nested/partial types, one type per file, DDD layering).

```bash
dotnet test Callora.Host.sln --configuration Release
# fast loop only, excluding the slow tier:
./scripts/dev-test.sh --filter "Category!=Slow"
```

### Slow (Testcontainers PostgreSQL)

Tests that need a real database are tagged `[Trait("Category","Slow")]` and spin
up PostgreSQL via `Testcontainers.PostgreSql` (Docker required). They create an
isolated temporary database per run. An optional
`CALLORA_TEST_POSTGRES_CONNECTION_STRING` points them at an existing server.

> **In-test builds:** any test that shells out to `dotnet build` must use
> `-nodeReuse:false` / `UseSharedCompilation=false` to avoid a locked build node.

### Frontend tests

The admin shell has its own Vitest suite (slots/hooks/services/loader + all admin
modules), run directly:

```bash
cd src/Administration/Resources/app/administration
npm ci && npm run test
```

## CI workflows

GitHub Actions, all third-party actions SHA-pinned.

### `.github/workflows/ci.yml`

Runs on push to `main` and on every pull request. Three jobs:

1. **Build & Test** — restore, `dotnet build ... --configuration Release`, then
   `dotnet test ... --collect:"XPlat Code Coverage"`. A **coverage ratchet gate**
   fails the build if line coverage drops below **25%**, computed over *all*
   coverage reports weighted by line count. (It used to read whichever report the
   filesystem returned first, which for a while meant the 282-line analyzer report
   instead of the 84,000-line core one — 93.6% announced, 33.6% actual.)
2. **Frontends** — a matrix over the surface runtime, the plugin frontends and the
   docs site: `npm ci`, audit of shipped dependencies, test where applicable, build.
3. **Admin Shell (Vitest + Build)** — `npm ci`, `npm run test`, `npm run build`
   in `src/Administration/Resources/app/administration` (Node 22). The Vitest gate
   keeps UI regressions off `main`; the build is the type/bundle check. It also
   regenerates the extension-point catalog and fails on a diff — a moved slot would
   otherwise leave `@callora/admin` promising a point that no longer exists.

Additional repo-wide quality automation (per `docs/QUALITY_STANDARDS.md`): CodeQL
(C# + JS/TS), Dependabot (nuget, npm, github-actions, weekly), and CycloneDX SBOMs.

### `.github/workflows/golden-path.yml`

Runs the chain a third-party plugin author walks, from outside: pack → `dotnet tool
install` → `plugin new` → `publish` → `test-contract` → `sign`, plus a counter-proof that
CAL0001 breaks the build of a plugin that only references the SDK package.

It exists because no test *inside* this repository crosses the package boundary. The first
run found four problems that the suite could never have caught — a security pin that never
reached the `nuspec`, an SDK that silently withheld its analyzers, a publish filter that
only worked at build time, and one that deleted the plugin's own assembly.

```bash
./scripts/golden-path.sh          # same run, locally
```

### `.github/workflows/docs.yml`

Three jobs on any change under `docs-site/**`, `docfx/**`, `src/**` or `README.md`:
**lint** (markdownlint + cspell), **assertions** (below), and **build** — which
produces the VitePress site and the DocFX .NET API reference, assembles them into
one artifact (the reference under `/api/`) and uploads it for Pages. The VitePress
build **fails on any internal dead link**, so a renamed page cannot merge silently.

The .NET API reference covers the platform packages only — plugins document their
own surface in their own repositories.

It also runs the **documentation assertions** — xUnit tests that read the docs, such as
"an entry type quoted for a shipped plugin matches its manifest". Those used to live only
in `ci.yml`, which ignores markdown; a docs-only change could break them with no job
running to notice.

```bash
dotnet tool restore
dotnet docfx docfx/docfx.json          # add --serve for a local preview
```

Deploy to GitHub Pages is **opt-in**: the deploy job runs only from `main` and
only when the `DOCS_DEPLOY` repository variable is set to `true`. The repo is
private today, so this is skipped by default (CI stays green). Enable it for the
public Community Edition (Settings → Pages → Source: GitHub Actions, then set
`DOCS_DEPLOY=true`).

### `.github/workflows/release.yml`

Runs on a pushed tag matching `v*`. It builds and tests in Release, packs the
framework packages, generates a CycloneDX SBOM for .NET, writes `SHA256SUMS` over
everything it attaches, and creates a GitHub release with generated notes, the
`.nupkg`/`.snupkg` files, the SBOM and the checksums.

A second job then composes those modules into `src/Host/Dev` and starts it against
a real Postgres, waiting for `/ready`. A release that publishes packages should
prove they are more than files, and readiness is the honest claim: the host
composed **and** reached its database.

::: info This repository does not release a runnable host
It used to publish `src/Core` as `callora-host.tar.gz`. Core has been
`OutputType=Library` since the module split, so that artifact had no entry point
and could not start. The runnable composition belongs to a distribution
(`callora-production`), which assembles these packages.
:::

### `.github/workflows/npm-publish.yml`

Publishes `@callora/surface` and `@callora/admin` on the same `v*` tag that drives
`release.yml`. Both halves come out of one release because they are two halves of one
contract: `@callora/surface` is the client half whose server half is
`Callora.Surface.Rendering`. If they drift apart, nobody notices except a customer.

Authentication is **trusted publishing** — npm exchanges the OIDC token granted by
`permissions: id-token: write` for a short-lived right to that one package. There is no
stored secret. It replaced a granular access token with 2FA bypass, which was needed
because the account is secured by a passkey and `--otp=` has nothing to offer there: a
permanent key that deliberately skips the check it was issued for.

Two things the workflow does that are easy to miss:

- **npm is upgraded to `@latest` first.** Trusted publishing needs 11.5.1 or newer, Node
  22 ships 10.9.x, and that version does not fail with a hint about itself — it fails with
  an ordinary 401, because the CLI does not know the OIDC path.
- **A version already in the registry is skipped.** npm rightly refuses to publish over an
  existing version, which would turn a release run red with nothing for anyone to fix.
  NuGet has `--skip-duplicate` for this; npm has no counterpart, so the workflow asks
  first.

Setup is per package on `npmjs.com/package/<name>/access` under **Trusted Publisher**:
this repository plus this workflow's filename. Both must match or the registry refuses.

## Versioning and releases

- **MinVer** derives the version from Git tags. Tag prefix is `v`; without a tag
  it falls back to `0.1.0-preview.0.<height>` (`MinVerMinimumMajorMinor=0.1`).
  **To release, push a tag** (`git tag v1.2.3 && git push origin v1.2.3`), which
  triggers `release.yml`.
- **Packaging.** Only projects that opt in with `<IsPackable>true</IsPackable>`
  produce a NuGet package (e.g. `Callora.Administration`,
  `Callora.Surface.Rendering`). The license is declared once, in
  `Directory.Build.props`: everything in this repository is Apache-2.0. It used to sit
  per-module, which is how four packages kept shipping `AGPL-3.0-or-later` for months
  after the switch.
  The colocated SPAs ship inside their packages as static web assets — a
  consuming host gets `/admin` and `/surface-app` automatically via the package
  reference.

## Distribution

Callora is distributed **self-contained via NuGet**: the `callora-production`
skeleton composes these packages into a single app container plus PostgreSQL, run
via docker-compose or on a VPS. See [Deployment](deployment.md) and
`docs/CALLORA_PLATFORM_BETRIEBSMODELL.md`.

> **Status:** A local NuGet feed backs the self-contained composition. The
> production feed, container image publishing, and the composed
> `callora-production` distribution are designed and partly validated (SWA-in-NuGet
> spike) but live in the separate composition repository, not here.
