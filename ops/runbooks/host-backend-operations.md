# Runbook: Callora Host Backend

Betriebshandbuch für den Host (PLAT-235). Zielgruppe: Betreiber ohne
Vorwissen über die Codebasis.

## Start & Umgebungen

- **Dev-Stack:** `docker start callora-backend-dev` (dotnet watch, Port 5000),
  Frontdoor über Caddy auf Port 8080, Shells auf 3200 (legacy-admin) und
  3300 (workspace). Host-Builds erfordern gestoppten Container
  (NuGet-obj-Race im Bind-Mount → NETSDK1064; danach ggf.
  `dotnet restore Callora.Host.sln --force`).
- **Produktion:** `dotnet publish src/Core` (siehe Release-Workflow);
  Konfiguration über `appsettings.json` + Umgebungsvariablen
  (`BackendHost__...`).
- **Pflicht vor Produktivstart:** `BackendHost:JwtSigningKey` setzen (der
  Dev-Default verweigert außerhalb von Development den Start), API-Keys und
  `DemoAdminUser:Password` ersetzen (Startup-Warnungen beachten),
  `AllowPrivateWebhookTargets=false` belassen.

## Health & Readiness

- `GET /health` — Liveness ohne Abhängigkeitsprüfung, Vertrag `{"status":"ok"}`.
- `GET /ready` — Readiness inklusive Datenbank.
- Observability: OTLP-Export über `Observability:OtlpEndpoint` (leer =
  deaktiviert). Metrik-Namespaces `callora.calls.*`, `callora.webhooks.*`,
  `callora.jobs.*`, `callora.plugin.lifecycle.*`.

## Datenbank & Migrationen

- EF-Migrationen liegen in `src/Core/Infrastructure/Persistence/Migrations`
  und werden beim Start angewendet.
- Neue Migration: Container stoppen, dann
  `dotnet ef migrations add <Name> --project src/Core/... --output-dir Infrastructure/Persistence/Migrations`.
- Backup vor Schema-Eingriffen: `pg_dump callora_host > backup.sql`.

## Schlüssel & Secrets

- DataProtection-Keyring liegt unter `BackendHost:DataProtectionKeysPath` —
  **im Backup einschließen**: ohne Keyring sind Webhook-Secrets,
  secret-typisierte Config-Werte und SIP-Passwörter nicht mehr entschlüsselbar.
- Key-Rotation: DataProtection rotiert automatisch; alte Keys nie löschen,
  solange verschlüsselte Werte existieren.

## Background-Jobs

- Tabelle `background_jobs`; Zustände Pending/Running/Succeeded/Failed.
- Stuck-Job-Recovery: Jobs, die in Running hängen (Prozess-Crash), werden
  nicht automatisch neu geplant → Status manuell auf Pending setzen:
  `UPDATE background_jobs SET "Status"=0 WHERE "Status"=1 AND "StartedAtUtc" < now() - interval '15 minutes';`
- Retention: `host.retention.cleanup` löscht abgeschlossene Jobs nach 14
  Tagen, Notifications nach 90 (Sektion `Retention` in appsettings).

## Plugins

- Verzeichnis `custom/plugins/<Name>` mit `registry.json`; Lifecycle über
  die Admin-API/-UI (install/activate/deactivate/uninstall, Hot-Swap).
- Shared-Contract-Assemblies (`"contracts": [...]`) sind bis zum Neustart
  gepinnt — Contract-Updates erfordern Host-Neustart.
- Signaturprüfung: `TrustedSigners`/`AllowUnsignedPlugins` (produktiv: false).

## Shutdown

- Graceful: Der Host legt beim Stop alle aktiven Calls auf (5-s-Budget) und
  beendet SSE-Streams sauber. `docker stop` (SIGTERM) genügt.

## Incident-Basics

- Logs: strukturiert (Console); Correlation über Trace-IDs (OTLP).
- 502 an der Frontdoor: Backend-Container läuft nicht oder Port 5000 belegt.
- „Route not mapped" für Workspaces: Workspace-Route-Cache prüfen —
  Workspace-Domänen werden über `/workspace/public/ui-chain` aufgelöst.
- Telefonie (SIP 4xx/5xx bei INVITE): Registrierung des Voice-Channels im
  Workspace prüfen (`/api/calls/channels?workspaceKey=...`).
