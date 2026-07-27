# Callora Framework

Modular .NET application framework for communication products: a domain-neutral
core plus an Administration UI, Workspaces, RBAC and a real backend/frontend
plugin model.

This repository is the **framework** — a set of packable libraries — not a
standalone host. The runnable process entrypoint and package composition live in
the separate **`callora-production`** repository, which assembles these packages
into a distribution (one app container + Postgres). The same framework can back
several distributions (community, enterprise, OEM/customer-specific hosts).

## Repository layout
- `src/Core` — domain-neutral platform core (packable library `Callora.Core`)
- `src/Administration` — Administration module + colocated Vue 3 admin shell
  (`Resources/app/administration`); ships its built SPA as a static web asset,
  served at `/admin` in the consuming host
- `src/Workspace` — Workspace module
- `src/Analyzers` — Roslyn analyzers guarding the public API surface
- `src/Host/Cli` — `callora` CLI (e.g. the plugin contract-test kit)
- `custom/static-plugins/*` — system-tier plugins (e.g. Communication)
- `custom/plugins/*` — dynamically installable plugins
- Composition / process entrypoint: external `callora-production` repository

## Build & test
```bash
dotnet restore Callora.Host.sln
dotnet build Callora.Host.sln          # also builds the admin SPA (vue-tsc + vite)
dotnet test Callora.Host.sln
```
The admin SPA is built by the .NET build; run its Vitest suite directly:
```bash
cd src/Administration/Resources/app/administration
npm ci && npm run test
```
Skip the frontend build (no Node required): `dotnet build -p:SkipAdminFrontend=true`.

## Administration (admin shell)
- Vue 3 SPA (Vite + TypeScript), colocated in
  `src/Administration/Resources/app/administration`.
- Management modules: users/operators, RBAC roles, plugins, workspaces (+ members),
  system configuration, media.
- Extensibility (Vue-native, not free component overrides): additive **Slots**,
  intervening **Hooks** (mutate/cancel/observe), controlled **Service-Overrides**.
- Plugin admin UIs are loaded at runtime by the micro-frontend loader (backend
  manifest → `/plugin-assets`), so install + activate + browser refresh surfaces
  a plugin's UI without a restart.

## Plugin signing
Installing a plugin is a fully privileged act — a plugin runs as host code and its
admin bundle as privileged admin-frontend code. The install gate verifies a detached
signature manifest (`plugin.signature.json`, ECDSA-P256) against a **trusted signer's
public key** and checks the covered file hashes (assembly + `registry.json`), so
capabilities and entry type are tamper-evident. Unsigned plugins are rejected unless
`BackendHost__AllowUnsignedPlugins=true`. Signature standing is shown per plugin in
Admin → Plugins.

- **Development**: the dev stack sets `BackendHost__AllowUnsignedPlugins=true`
  (`docker-compose.yml`), so locally built plugins load without signing.
- **Production**: `AllowUnsignedPlugins` is `false` — every deployed plugin (including
  bundled system-tier plugins under `custom/static-plugins/`) must be signed and its
  signer trusted, or it will not load.

Signing a plugin for production:
```bash
# 1) Generate an ECDSA P-256 keypair (keep the private key secret; publish the public key)
openssl ecparam -name prime256v1 -genkey -noout -out callora-signing.key.pem
openssl ec -in callora-signing.key.pem -pubout -out callora-signing.pub.pem

# 2) Build the plugin, then sign its directory (writes plugin.signature.json next to registry.json)
dotnet build custom/static-plugins/Communication/Callora.Plugin.Communication.csproj
callora plugin sign --plugin <plugin-directory> --key callora-signing.key.pem
```
Then trust the signer in the host config (the fingerprint is derived from the key):
```jsonc
"BackendHost": {
  "TrustedSigners": [
    { "publisherId": "callora", "displayName": "Callora", "publicKey": "-----BEGIN PUBLIC KEY-----\n…\n-----END PUBLIC KEY-----" }
  ]
}
```
Revoke a compromised signer or a specific bad build via `BackendHost__RevokedSignerFingerprints`
/ `BackendHost__RevokedContentHashes` (enforced at install and, through runtime
rehydration, at load). Signing Callora's own system plugins in the release pipeline is
tracked as an operations task.

## Environment
- Template: `.env.example`
- Local file: `.env` (is ignored by git)
- ASP.NET configuration keys use double underscore mapping, e.g. `BackendHost__DatabaseConnectionString`.

Beispiel fuer lokale Shell-Session:
```bash
set -a
source .env
set +a
dotnet run --project src/Core/Callora.Core.csproj
```

Lokalen Dev-Stack (Backend + Postgres) starten:
```bash
docker compose -f docker-compose.dev.yml up -d
```

API ist danach erreichbar unter:

- `http://localhost:5000/health`

## Knowledge Base
- Zentraler Einstieg: `docs/HOST_KNOWLEDGE_INDEX.md`
- Zielbild: `Callora_Targetstruktur_fuer_KI.md`
- Host API: `docs/portal/architecture/host-backend-api.md`
- Pluginmodell: `docs/portal/modules/plugins.md`
- Compliance: `docs/compliance/COMPLIANCE_BASELINE_DSGVO_EU_AI_ACT.md`
- Lokales Env-Setup: `docs/LOCAL_ENVIRONMENT.md`

## Notes
- Backend API supports the plugin lifecycle (`install/activate/deactivate/uninstall`) and `install/nuget`.
- Admin shell source lives in `src/Administration/Resources/app/administration` (colocated) and ships with the Administration package.
- The tenant surface runtime is server-side rendered in `src/Surface.Rendering` (colocated Vue at `Resources/app/surface`, served at `/surface-app`).

## Communication Plugin (System-Tier)
- Projekt: `custom/static-plugins/Communication/Callora.Plugin.Communication.csproj`
- Entrypoint: `Callora.Plugin.Communication.Application.CommunicationPlugin`
- Registry: `custom/static-plugins/Communication/registry.json` (pluginId `communication`, tier `system`)
- Öffentliche Verträge: `custom/static-plugins/Communication/Abstractions` (`Callora.Plugin.Communication.Abstractions`)

Build + Install-Beispiel:
```bash
dotnet build custom/static-plugins/Communication/Callora.Plugin.Communication.csproj
curl -s -X POST http://localhost:5000/api/plugins/install \
  -H "X-Callora-Api-Key: callora-local-dev-key-change-me" \
  -H "Content-Type: application/json" \
  -d '{"assemblyPath":"/abs/path/to/Callora.Plugin.Communication.dll"}'
```

Contract-Test-Kit (lokal/CI):
```bash
dotnet run --project src/Host/Cli/Callora.Host.Cli.csproj -- \
  plugin test-contract \
  --assembly custom/static-plugins/Communication/bin/Debug/net10.0/Callora.Plugin.Communication.dll \
  --registry custom/static-plugins/Communication/registry.json
```
