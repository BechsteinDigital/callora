# Baustein 2 — Callora-Production self-contained + Dockerfile + Compose (Plan)

Datum: 2026-07-18 · Baustein 2/4 der Callora-Production-Setup-Spec
Repo: `/home/dbechstein/Projekte/Callora-Production` (separates git-Repo, `main`)

## Ziel

Callora-Production baut nicht mehr per ProjectReference in den Monorepo, sondern
konsumiert Callora.* als NuGet-Pakete (Version `0.1.0-local`) aus dem in Baustein 1
gebauten Feed. Ein multi-stage Dockerfile publisht die App gegen den Feed; ein
docker-compose bringt App + Postgres hoch. Durchstich: `docker compose up` →
`/admin` lädt, Login mit DemoAdmin funktioniert.

## Kernentscheidungen (DECISION-Log)

1. **Feed im Docker-Kontext = In-Repo `local-packages/` (git-ignored):** Der
   Docker-Build-Kontext kann nicht auf `../callora/artifacts/nuget-local`
   zugreifen (außerhalb des Kontexts). Ein Skript `scripts/sync-packages.sh`
   kopiert die 4 `.nupkg` aus dem callora-Feed nach `local-packages/`. `nuget.config`
   zeigt auf `./local-packages` → derselbe Pfad wirkt für lokalen `dotnet build`
   UND `docker build`. *Öffentlicher Feed = Nicht-Ziel (Spec); erster Wurf lokal.*

2. **Communication.Abstractions als PackageReference mit rein:** trägt den
   `ICommunicationChannelRegistry`-Typ in die Default-ALC des Prod-Hosts. Ohne
   diese Referenz lädt ein Communication-Plugin den Typ in seiner eigenen ALC →
   Typidentitätsbruch. Der CLI-Host hält dieselbe Referenz (Vorbild). Das Plugin
   selbst wird NICHT mitgeliefert (Discovery-Sache, späteres Thema).

3. **DB-Host über Compose-Env, nicht appsettings ändern:** `appsettings.json`
   trägt `Host=localhost` (für lokalen Lauf). Im Compose überschreibt
   `BackendHost__DatabaseConnectionString=Host=db;…` auf den Postgres-Service.
   Die Secret-Hygiene (JWT-Key, DemoAdmin dev-only, …) bleibt **Baustein 3** —
   hier nur die Compose-Verdrahtung, damit der Boot gelingt.

4. **Runtime-Image ohne Node:** die Admin-SPA reist als SWA im Administration-nupkg
   (Baustein 1) → der Prod-Build braucht kein npm, nur `dotnet publish`. Stage 2 =
   schlankes `aspnet:10.0`-Runtime-Image.

## Dateien (Callora-Production-Repo)

- `nuget.config` (neu): `./local-packages` + nuget.org, `<clear/>`.
- `scripts/sync-packages.sh` (neu): räumt `local-packages/`, kopiert die 4 nupkg
  aus `../callora/artifacts/nuget-local` (Pfad überschreibbar via `$CALLORA_FEED`).
- `Callora.Production.csproj` (mod): 3 ProjectReferences → 4 PackageReferences
  (Core/Administration/Workspace/Communication.Abstractions, `0.1.0-local`).
- `Dockerfile` (neu): Stage build (`sdk:10.0`, COPY nuget.config+local-packages+
  Projekt, restore, publish) → Stage runtime (`aspnet:10.0`, COPY --from, ENTRYPOINT).
- `.dockerignore` (neu): bin/obj/.git/… raus; `local-packages/` MUSS rein (Feed).
- `docker-compose.yml` (neu): `app` (build: ., ports 8080:8080, env DB-Host +
  ASPNETCORE_URLS, depends_on db healthy) + `db` (postgres:17, Volume, healthcheck).
- `.gitignore` (mod): `local-packages/` ergänzen.
- `README.md` (mod): Dev- + Betreiber-Kurzanleitung (Kern kommt in Baustein 4).

## Arbeitsschritte

1. Feature-Branch `feat/self-contained-nuget` im Callora-Production-Repo.
2. `nuget.config` + `scripts/sync-packages.sh`; Skript ausführen → 4 nupkg in
   `local-packages/`.
3. csproj: ProjectReference → PackageReference. `dotnet restore` + `dotnet build`
   lokal gegen den Feed (Beweis: baut ohne Monorepo).
4. `Dockerfile` + `.dockerignore` + `docker-compose.yml`.
5. `docker compose build` → `docker compose up -d`.
6. Boot-Verify: `/admin` liefert SPA (HTTP 200); Login `POST /api/auth/login`
   mit DemoAdmin (`admin` / `admin123!`) liefert ein Token/Cookie; Health/Startup ok.
7. `docker compose down -v`; Reviewer; Merge.

## Durchstich-Kriterium (Spec-Erfolgskriterium 2 + 3)
Callora-Production baut ausschließlich gegen den lokalen Feed (kein ProjectReference
mehr); `docker compose up` bringt App + Postgres hoch; `/admin` lädt; Login gelingt.

## Risiken
- **DB-Migration beim Boot:** braucht Postgres *vor* der App → `depends_on:
  condition: service_healthy` + Postgres-Healthcheck.
- **Port-/Host-Binding:** App muss auf `0.0.0.0:8080` lauschen (ASPNETCORE_URLS),
  nicht localhost, sonst kein Zugriff aus dem Host-Netz.
- **Communication.Abstractions-Trim:** PackageReference ohne direkte Nutzung darf
  nicht wegoptimiert werden — bei nicht-getrimmtem publish landet jede Referenz im
  Output; im Boot verifizieren (Assembly in /app).
