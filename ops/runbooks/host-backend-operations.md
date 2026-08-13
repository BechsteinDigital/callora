# Runbook: Callora Host Backend

Betriebshandbuch für den Host (PLAT-235). Zielgruppe: Betreiber ohne
Vorwissen über die Codebasis.

## Start & Umgebungen

- **Dev-Stack:** `docker start callora-backend-dev` (dotnet watch, Port 5000),
  Frontdoor über Caddy auf Port 8080. Ein Prozess bedient alles: die Administration
  unter `/admin` (colocated im Administration-Modul) und die öffentliche Fläche über
  `Callora.Surface.Rendering`. Separate Shell-Prozesse auf 3200/3300 gab es früher,
  gibt es nicht mehr. Host-Builds erfordern gestoppten Container (NuGet-obj-Race im
  Bind-Mount → NETSDK1064; danach ggf.
  `dotnet restore Callora.Host.sln --force`).
- **Produktion:** Nicht aus diesem Repository. `src/Core` ist seit dem
  Modul-Split `OutputType=Library` und hat keinen Einstiegspunkt — ein
  `dotnet publish` darauf erzeugt nichts Startbares. Der ausliefernde Host
  komponiert die Pakete selbst (`callora-production`); der Release-Workflow
  packt hier nur die Pakete. Konfiguration über `appsettings.json` +
  Umgebungsvariablen (`BackendHost__...`).
- **Pflicht vor Produktivstart:** `BackendHost:JwtSigningKey` setzen (der
  Dev-Default verweigert außerhalb von Development den Start), API-Keys ersetzen,
  `AllowPrivateWebhookTargets=false` belassen. Für den Erstzugang
  `BackendHost:InitialOperator` setzen — er seedet einmalig auf leerer Datenbank und
  fasst danach nichts mehr an; nach der ersten Anmeldung Passwort ändern,
  `Enabled=false` setzen und die Zugangsdaten aus der Konfiguration entfernen
  (Startup-Warnungen beachten).

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

- DataProtection-Keyring liegt in der **Datenbank** (PLAT-232,
  `PersistKeysToDbContext`) — mehrinstanzfähig. Das Datenbank-Backup deckt ihn
  damit ab; ohne Keyring sind Webhook-Secrets, secret-typisierte Config-Werte
  und SIP-Passwörter nicht mehr entschlüsselbar.
- `BackendHost:DataProtectionKeysPath` ist kein Speicherort mehr, sondern nur
  die **Import-Quelle**: Der DB-Init-Service zieht dort einmalig Altschlüssel
  (`key-*.xml`) in die Datenbank. Ein Backup dieses Verzeichnisses schützt
  nach dem Import nichts mehr.
- Key-Rotation: DataProtection rotiert automatisch; alte Keys nie löschen,
  solange verschlüsselte Werte existieren.

## Background-Jobs

- Tabelle `background_jobs`; Zustände Pending/Running/Succeeded/Failed.
- Stuck-Job-Recovery läuft **automatisch**, nichts ist von Hand zu tun: Jeder
  Job hält beim Anlauf ein Lease. Läuft es ab (Prozess-Crash), nimmt
  `TryClaimNextDueAsync` den Job wieder auf, solange sein Versuchsbudget nicht
  erschöpft ist; ist es erschöpft, setzt `FailExpiredExhaustedAsync` ihn auf
  Failed. Beides passiert in jedem Worker-Durchlauf.
- **Kein manuelles `UPDATE ... SET Status=0`.** Hier stand einmal ein SQL, das
  jeden seit 15 Minuten laufenden Job auf Pending zurücksetzte. Es unterscheidet
  nicht zwischen einem toten Job und einem, der legitim lange läuft und ein
  gültiges Lease hält — der wird dann ein zweites Mal ausgeführt. Genau das
  verhindert der Fencing-Token, und ein Schreibzugriff an der Anwendung vorbei
  hebelt ihn aus.
- Hängt ein Job sichtbar: `LeaseExpiresAtUtc`, `AttemptCount` und `MaxAttempts`
  ansehen. Läuft das Lease nicht ab, lebt der Worker noch.
- Retention: `host.retention.cleanup` löscht abgeschlossene Jobs nach 14 Tagen
  (`Retention:CompletedJobRetention`) und Notifications nach 90
  (`Retention:NotificationRetention`).

## Plugins

- Verzeichnis `custom/plugins/<Name>` mit `registry.json`; Lifecycle über
  die Admin-API/-UI (install/activate/deactivate/uninstall, Hot-Swap).
- Shared-Contract-Assemblies (`"contracts": [...]`) sind bis zum Neustart
  gepinnt — Contract-Updates erfordern Host-Neustart.
- Signaturprüfung: `TrustedSigners`/`AllowUnsignedPlugins` (produktiv: false).

## Shutdown

- Graceful: Der Host lässt langlebige Plugins vor dem Stop **leerlaufen**
  (ADR-018). Ein Plugin, das `IDrainablePlugin` implementiert, nimmt keine neue
  Arbeit mehr an und meldet sich zurück, wenn die laufende fertig ist —
  Communication wartet damit auf aktive Anrufe, statt sie aufzulegen.
- Die Frist gehört dem Host: `CalloraHosting:PluginDrainTimeout`, Vorgabe
  **30 Sekunden**. Läuft sie ab, wird das Plugin trotzdem gestoppt (mit einer
  Warnung im Log) — ein Drain kann eine Deaktivierung verzögern, nie verhindern.
- Draining läuft vor dem Zurückziehen der Exports und vor `StopAsync`, weil die
  noch laufende Arbeit sie braucht. `docker stop` (SIGTERM) genügt; das
  Stop-Timeout der Umgebung muss über `PluginDrainTimeout` liegen, sonst
  schneidet der Container-Runtime das Draining ab.

## Incident-Basics

- Logs: strukturiert (Console); Correlation über Trace-IDs (OTLP).
- 502 an der Frontdoor: Backend-Container läuft nicht oder Port 5000 belegt.
- „Route not mapped" für Workspaces: Workspace-Route-Cache prüfen —
  Workspace-Domänen werden über `/workspace/public/ui-chain` aufgelöst.
- Telefonie (SIP 4xx/5xx bei INVITE): Registrierung des Voice-Channels im
  Workspace prüfen (`/api/calls/channels?workspaceKey=...`).
