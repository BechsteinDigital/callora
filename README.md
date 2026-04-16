# Callora Host

Standalone host platform repository for Callora.

## Scope
- Host backend API (`src/Host/Backend`)
- Host plugin contracts (`src/Host/PluginContracts`)
- Hosting/runtime lifecycle (`src/Hosting`)
- Module abstractions used by host runtime (`src/Abstractions`)

## Build
```bash
dotnet restore Callora.Host.sln
dotnet build Callora.Host.sln
dotnet test tests/Callora.Host.Backend.Tests/Callora.Host.Backend.Tests.csproj
```

## Knowledge Base
- Zentraler Einstieg: `docs/HOST_KNOWLEDGE_INDEX.md`
- Zielbild: `Callora_Targetstruktur_fuer_KI.md`
- Host API: `docs/portal/architecture/host-backend-api.md`
- Pluginmodell: `docs/portal/modules/plugins.md`
- Compliance: `docs/compliance/COMPLIANCE_BASELINE_DSGVO_EU_AI_ACT.md`

## Notes
- This split keeps current project references local.
- Later, `src/Abstractions` can become its own package if desired.
- Host API supports plugin lifecycle (`install/activate/deactivate/uninstall`) and `install/nuget`.

## VoIP Plugin (neu)
- Projekt: `src/Plugins/Voip/Callora.Plugins.Voip.csproj`
- Entrypoint: `Callora.Plugins.Voip.Application.VoipPlugin`
- Registry: `src/Plugins/Voip/registry.json`

Build + Install-Beispiel:
```bash
dotnet build src/Plugins/Voip/Callora.Plugins.Voip.csproj
curl -s -X POST http://localhost:5000/api/plugins/install \
  -H "X-Callora-Api-Key: callora-local-dev-key-change-me" \
  -H "Content-Type: application/json" \
  -d '{"assemblyPath":"/abs/path/to/Callora.Plugins.Voip.dll"}'
```
