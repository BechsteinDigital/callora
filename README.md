# Callora Host

Standalone host platform repository for Callora.

## Scope
- Host backend API (`src/Core`)
- Host plugin contracts (`src/Host/PluginContracts`)
- Hosting/runtime lifecycle (`src/Hosting`)
- Module abstractions used by host runtime (`src/Abstractions`)

## Build
```bash
dotnet restore Callora.Host.sln
dotnet build Callora.Host.sln
./scripts/build-admin-ui.sh
dotnet test tests/Callora.Core.Tests/Callora.Core.Tests.csproj
```

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
- This split keeps current project references local.
- Later, `src/Abstractions` can become its own package if desired.
- Host API supports plugin lifecycle (`install/activate/deactivate/uninstall`) and `install/nuget`.
- Admin UI shell source is located in `apps/admin-shell` and is deployed separately from the backend host.
- Workspace shell source is located in `apps/workspace-shell` and is deployed separately from the backend host.

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
