# ADR-011: Separate Shell Hosts und Resources/Public-Restructure

Status: Accepted  
Date: 2026-04-22

## Context

Callora folgt dem Shopware-Prinzip (Host als Control-Plane, Plugins als Erweiterungen), soll aber fuer Admin und Workspace nicht monolithisch ueber das Backend-`wwwroot` betrieben werden.

Aktueller Stand:

1. Admin/Workspace Shell Assets werden teilweise in `src/Host/Backend/wwwroot` kopiert.
2. Plugin-UI-Manifesteintraege referenzieren noch Source-nahe Pfade.
3. UI-Projekte liegen im C#-`src`-Baum (`src/Host/AdminUi`), was technische Verantwortung mischt.

Ziel:

1. Betriebsmodell 2: separate Shell Hosts fuer `Admin UI` und `Workspace UI`.
2. Strikte Trennung von Plugin-`Resources` Source und Public Build-Artefakten.
3. Deterministische Asset-Manifeste als einzige Runtime-Wahrheit.

## Decision

### 1) Betriebsmodell

1. `Admin UI` und `Workspace UI` werden separat deployt und separat gehostet.
2. `Host Backend` bleibt API-/Control-Plane und liefert keine Shell-Builds mehr als Primaerquelle.
3. Backend-`wwwroot` bleibt fuer backendnahe statische Inhalte zulaessig, aber nicht als Hauptauslieferung fuer Shells.

### 2) Zielstruktur Repository

```text
/
├── src/                              # nur C# und Host-Runtime
│   ├── Host/
│   │   ├── Backend/
│   │   ├── Cli/
│   │   └── PluginContracts/
│   ├── Hosting/
│   └── Abstractions/
│
├── apps/                             # Frontend-Shells
│   ├── admin-shell/
│   └── workspace-shell/
│
├── custom/plugins/<Plugin>/src/Resources/
│   ├── app/
│   │   ├── admin/src/               # plugin source code admin
│   │   └── workspace/src/           # plugin source code workspace
│   ├── views/
│   │   └── workspace/               # workspace template source
│   └── public/
│       ├── admin/                   # built assets
│       └── workspace/               # built assets
│
├── build/manifests/                  # generated manifests
└── scripts/
```

### 3) Plugin Asset Pipeline (Admin + Workspace)

1. Plugin-UI Source wird pro Surface gebaut (`app/admin/src`, `app/workspace/src`).
2. Build-Output wird nach `Resources/public/<surface>` geschrieben.
3. Manifeste enthalten nur final ladbare Public-Artefakte (kein `.ts`/Source-Pfad).
4. Shells laden ausschliesslich ueber Manifest + Public URL Base.

### 4) URL- und Hosting-Modell

1. Externes Zielbild ist Shopware-analog pro Workspace-Domain:
   - eine Origin pro Workspace (z. B. `workspace-a.example.com`)
   - `admin` unter Pfad (`/admin`)
   - API-Segmente unter derselben Origin (`/api`, `/workspace`)
2. Intern duerfen Admin- und Workspace-Shell als getrennte Deployables/Services laufen.
3. `ADMIN_SHELL_BASE_URL`: Basis-URL oder Pfad fuer Admin Shell (Default: `/admin/`).
4. `WORKSPACE_SHELL_BASE_URL`: Basis-URL oder Pfad fuer Workspace Shell (Default: `/`).
5. `PLUGIN_ASSET_BASE_URL`: Basis-URL fuer Plugin Public Assets.
6. `API_BASE_URL`: Host API fuer Shells.

### 4.1) Redirect-/Proxy-Strategie (verbindlich)

1. Backend liefert keine Shell-HTML-Runtime mehr als Primaerstrategie.
2. Frontdoor/Gateway (Ingress/Proxy/LB) ist die primäre Routing-Instanz fuer Host+Path:
   - `/admin` und `/admin/**` -> Admin-Shell
   - `/api/**` -> Host Admin API
   - `/workspace/**` -> Host Workspace API
   - `/**` (rest) -> Workspace-Shell
3. Backend-Redirects bleiben zulaessig als Fallback/Dev-Hilfe, duerfen aber das Frontdoor-Routing nicht unterlaufen.
4. Plugin-Assets und Manifeste werden ueber `PLUGIN_ASSET_BASE_URL` bzw. `PLUGIN_MANIFEST_URL` geladen.
5. Gateway/Ingress muss Ziel-Origins und Cookies konsistent halten (SameSite/CORS/Forwarded Headers).
6. Mehrere Workspace-Domains werden ueber DNS -> gleiche Frontdoor-IP aufgeloest; Workspace-Aufloesung erfolgt im Host ueber `requestHost + requestPath`.

### 5) Guardrails

1. Keine Runtime-Auslieferung von Plugin-Quellcode aus `Resources/app/*/src`.
2. Kein Runtime-Manifest mit lokalen Source-Pfaden.
3. Plugin-Assets duerfen RBAC/Entitlement nur erweitern, nicht umgehen.
4. Shells duerfen ohne funktionierende Plugin-Assets weiterlaufen (degradierbar, nicht global blockierend).

## Consequences

Positive:

1. Saubere Trennung von C# Runtime und Frontend Deployables.
2. Naeher am Shopware-Pattern: Source vs Public Assets, klare Extension-Surfaces.
3. Bessere Skalierbarkeit fuer Admin/Workspace (CDN/Edge moeglich).

Tradeoffs:

1. Mehr Deploy-Artefakte und Umgebungsvariablen.
2. Hoehere Anforderungen an CORS/Cookie/Auth-Konzept.
3. Build-Pipeline wird strikter (Manifest-Gates, Build-Reihenfolge).

## Refactoring Checklist

### Phase 0: Standards und Gating

1. Verbindliches Manifest-Schema festlegen (`admin`, `workspace` getrennt).
2. CI-Check einfuehren: keine Source-Pfade (`/src/Resources/app/...`) im Manifest.
3. CI-Check einfuehren: nur `.js`, `.mjs`, `.css` als Runtime-Assets.

### Phase 1: Shell-Projekte aus `src` auslagern

1. `src/Host/AdminUi` nach `apps/admin-shell` migrieren.
2. `src/Host/WorkspaceUi` (oder Aequivalent) nach `apps/workspace-shell` migrieren.
3. Build-Skripte auf neue Pfade umstellen.
4. Backend-Fallback auf Shell-Dateien schrittweise entfernen oder nur als Dev-Option belassen.

### Phase 2: Plugin Resources/Public trennen

1. Plugin-Konvention dokumentieren:
   - `Resources/app/<surface>/src` als Source
   - `Resources/public/<surface>` als Build-Output
2. `scripts/build-plugin-ui-assets.sh` auf Compile-Output statt Source-Copy umbauen.
3. Template-Handling fuer Workspace in `Resources/views/workspace` beibehalten, aber Ausgabe unter versionierter Public-Strategie dokumentieren.

### Phase 3: Manifest und Loader umstellen

1. Manifeste unter `build/manifests/` erzeugen.
2. Loader in Admin/Workspace Shell auf neue Manifestfelder migrieren (Public URL statt Source-Pfadnormalisierung).
3. Fehlertoleranz pro Plugin-Asset beibehalten (ein defektes Plugin blockiert nicht alle).

### Phase 4: Deployment Split aktivieren

1. Admin Shell Deployment-Pipeline separieren.
2. Workspace Shell Deployment-Pipeline separieren.
3. Plugin-Public-Assets Deployment-Pipeline separieren.
4. API-Gateway/CORS/Cookie-Konfiguration fuer Multi-Domain + Single-Origin-pro-Workspace produktionsfaehig machen.
5. CI-Gate: `scripts/dev-build.sh` baut standardmaessig `admin-shell` und `workspace-shell` (inkl. `npm install/ci` + `npm run generate`) und darf diese Schritte nicht stillschweigend ueberspringen.

### Phase 5: Aufraeumen und Stabilisieren

1. Legacy-Kopierpfade nach `src/Host/Backend/wwwroot/admin` und `.../workspace` entfernen.
2. Dokumentation (`README`, `LOCAL_ENVIRONMENT`, Runbooks) auf neue Struktur aktualisieren.
3. Smoke-Tests:
   - Admin ohne Plugin-Assets startet.
   - Workspace ohne Plugin-Assets startet.
   - Plugin-Assets werden ueber Manifest korrekt geladen.

## Acceptance Criteria

1. `src/` enthaelt keine Frontend-Shell-Quellen mehr.
2. Keine Source-Pfade in Runtime-Manifesten.
3. Shells sind ueber eigene Base URLs deploybar.
4. Backend funktioniert als API-Control-Plane ohne Shell-Copy-Zwang.
5. Plugin-Admin- und Plugin-Workspace-Erweiterungen laufen ueber Public-Artefakte reproduzierbar.
6. Workspace-Domain-Routing ist host-basiert und fuer mehrere Workspace-Domains auf einer Plattforminstanz nutzbar.
