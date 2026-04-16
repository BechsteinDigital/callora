# Host Backend API

Der Host folgt einem Shopware-aehnlichen Muster:

- schlanker Kernel (`Callora.Hosting`)
- persistenter Extension-Lifecycle
- API-first fuer Install/Activate/Deactivate/Uninstall

## Aktueller Mindeststandard (Phase 1)

- Runtime-Lifecycle via `IHostPluginLifecycle`
- Persistente Plugin-Entity-Registry (SQLite)
- Audit-Trail fuer alle Lifecycle-Operationen
- API-Key-Authentifizierung fuer Plugin-Endpunkte
- Swagger/OpenAPI fuer schnelle Integrationssicht

## Endpunkte

- `GET /health` (anonym)
- `GET /api/plugins`
- `GET /api/plugins/installed`
- `GET /api/plugins/audit`
- `POST /api/plugins/install`
- `POST /api/plugins/install/nuget`
- `POST /api/plugins/{pluginId}/activate`
- `POST /api/plugins/{pluginId}/deactivate`
- `DELETE /api/plugins/{pluginId}`

Alle `/api/plugins/*` Endpunkte sind auth-pflichtig.

Install-Requests koennen `entryTypeName` weglassen, wenn neben der Assembly
eine gueltige `registry.json` liegt.

NuGet-Install nutzt den lokalen NuGet-Cache als Paketquelle und resolved daraus
die Plugin-Assembly (optional ueber `assemblyFileName`).

## Sicherheitsmodell (jetzt)

- Header: `X-Callora-Api-Key`
- Konfiguration: `BackendHost:ApiKeys`
- Mindestziel: kein ungeschuetzter Lifecycle-Zugriff in Runtime-Umgebungen

Naechste Stufe:

- Tenant- und Rollenmodell
- kurzlebige Tokens (OIDC/JWT)
- API-Key-Rotation und Scope-Modell

## Persistenzmodell (jetzt)

DB: `BackendHost:DatabasePath` (SQLite in Phase 1)

- `plugin_installations`
- `plugin_audit_logs`

Damit sind Runtime-Zustand und installierter Zustand getrennt sichtbar:

- Runtime: `GET /api/plugins`
- Persistente Registry: `GET /api/plugins/installed`

## Ausrichtung zur Zielstruktur

Diese Stufe ist das Fundament fuer:

- Paket-Signatur und Vertrauenskette
- Host-verwaltete Install/Update/Deactivate/Uninstall-Prozesse
- Tenant-/Lizenz-Entitlements vor Aktivierung
- DSGVO- und EU-AI-Act-konforme Nachvollziehbarkeit im Host
