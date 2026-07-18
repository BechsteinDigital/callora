# Callora-Production Setup (Design)

Datum: 2026-07-18 · Status: Design (Strategie validiert, zur Freigabe)

## Kontext

Callora-Production (`/home/dbechstein/Projekte/Callora-Production`, separates git-Repo) ist das
Distributions-Skelett analog `shopware/production`: ein Thin-Host (`Program.cs` komponiert
Core/Administration/Workspace), der die Plattform aktuell per **ProjectReference** in den
Nachbar-Monorepo `callora/` einbindet. Heute fehlt ihm das komplette Deployment-Gerüst
(kein Dockerfile, kein compose, kein `.env`), und `appsettings.json` trägt alle Secrets
im Klartext eingecheckt (JWT-Key `change-me`, DB-Passwort, DemoAdmin `admin123!`).

Der Monorepo hat bereits einen ausgereiften **Dev-Stack** (`docker-compose.yml`, SDK-Image +
`dotnet watch` + Source-Mount + Postgres) für die Plattform-Entwicklung sowie ein veraltetes
`docker-compose.prod-like.yml` (4 Container: Backend + separate Nuxt-Shells + Caddy) auf der
**alten** Architektur.

## Zielbild

Ein Betreiber klont **nur** Callora-Production, `docker compose up`, und hat Backend +
Admin-Shell + Postgres unter seiner Domain. Ein Entwickler dasselbe lokal. Zwei tragende
Vereinfachungen:

- **Colocated Admin-Shell** → der separate admin-shell-Container entfällt. Das Deployment
  kollabiert auf **1 App-Container + Postgres** (statt der alten vier).
- **Self-contained über NuGet** → Callora-Production referenziert Callora.* als **NuGet-Pakete**
  (lokaler Feed genügt), nicht mehr den Monorepo. Das Image braucht nur dieses eine Repo.

## Validierung (Spike, 2026-07-18)

`dotnet pack` von `Callora.Administration` mit `-p:IsPackable=true` erzeugt ein nupkg, das
`staticwebassets/admin/index.html` + `staticwebassets/admin/assets/*.js|css` (die komplette
Admin-SPA) **und** `build/…StaticWebAssets.props` enthält. Ein konsumierender Host bekommt die
Assets damit automatisch unter `/admin` — der colocated-Ansatz ist NuGet-tauglich. Vier
Konfig-Punkte für den echten Bau:

1. `<IsPackable>true</IsPackable>` (Web-SDK-Default ist false).
2. Der Build-Output-Ignore darf **keine** `wwwroot/.gitignore` sein (bricht pack mit NU5119);
   stattdessen z. B. über die Frontend-`.gitignore` oder ein pack-Exclude lösen.
3. Die Frontend-**Quelle** (`Resources/app/administration/`) aus dem Paket ausschließen
   (`content/Resources/` tauchte im Spike-Paket auf) — nur die gebauten SWA gehören rein.
4. SWA-Props kommen automatisch; Konsum ist Standard-.NET (PackageReference).

## Zerlegung (je eigener Design→Plan→Bau-Zyklus)

1. **NuGet-Paketierung + lokaler Feed** *(riskanter Kern, validiert)*: Core/Administration/Workspace
   als `.nupkg` (Admin-SPA als SWA im Administration-Paket), plus ein lokaler Feed
   (`nuget.config` + Verzeichnis). Durchstich: ein Test-Host konsumiert die Pakete aus dem Feed,
   `/admin` lädt. Hier hängt auch das ALC-/`Communication.Abstractions`-Typidentität-TODO.
2. **Callora-Production self-contained**: `ProjectReference` → `PackageReference`; multi-stage
   **Dockerfile** (restore aus Feed → publish → schlankes ASP.NET-Runtime-Image);
   **docker-compose** (App + Postgres).
3. **Config-Hygiene** *(orthogonal, sicherheitsrelevant)*: Secrets aus `appsettings.json` →
   Umgebungsvariablen/`.env`; `.env.example`; sichere Prod-Defaults (JWT-Key erzeugen statt
   `change-me`, DemoAdmin nur im Dev-Profil, `AuthCookieRequireHttps=true` in Prod,
   API-Key/DB-Passwort verpflichtend gesetzt).
4. **TLS + Onboarding**: Reverse-Proxy (Caddy/Traefik) im compose mit auto-HTTPS pro Domain;
   README für beide Personas (Dev: `docker compose up`; Betreiber: `.env` setzen, Domain, up).

**Reihenfolge:** 1 → 2 → 3 → 4. Baustein 1 zuerst, weil er die riskante Voraussetzung ist
(bereits per Spike als machbar bestätigt).

## Nicht-Ziele

- Kubernetes/Helm, managed PaaS (bewusst Docker-Compose/VPS gewählt).
- Öffentlicher NuGet-Feed / CI-Publishing (lokaler Feed genügt für den ersten Wurf).
- Multi-Node-HA, externes Secret-Management (Vault o. ä.) — später, use-case-getrieben.
- Die Workspace-Storefront-Auslieferung als eigener Container (läuft im selben App-Host mit;
  separater Surface-Betrieb ist ein späteres Thema).

## Erfolgskriterien

1. `dotnet pack` erzeugt für alle drei Module valide `.nupkg` (Admin-SPA als SWA im Administration-Paket).
2. Callora-Production baut ausschließlich gegen den lokalen Feed (kein ProjectReference mehr).
3. `docker compose up` in Callora-Production bringt App + Postgres hoch; `/admin` lädt, Login funktioniert.
4. Keine Secrets mehr im eingecheckten `appsettings.json`; `.env.example` dokumentiert alle nötigen Werte.
5. README führt Entwickler und Betreiber jeweils in wenigen Schritten zum laufenden System.
