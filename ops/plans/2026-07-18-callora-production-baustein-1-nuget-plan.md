# Baustein 1 — NuGet-Paketierung + lokaler Feed (Plan)

Datum: 2026-07-18 · Baustein 1/4 der Callora-Production-Setup-Spec
(`ops/specs/2026-07-18-callora-production-setup-design.md`)

## Ziel

Core / Administration / Workspace **und** den Communication-Contract als valide
`.nupkg` erzeugen, in einen lokalen Feed legen, und mit einem Wegwerf-Test-Host
beweisen: ein Host konsumiert die Pakete rein aus dem Feed und `/admin` lädt.
Kein Umbau von Callora-Production (das ist Baustein 2).

## Kernentscheidungen (DECISION-Log)

1. **Feed-Ort:** `artifacts/nuget-local/` im callora-Repo, git-ignored. Ein
   Skript `scripts/pack-local.sh` packt alle Module dorthin. Callora-Production
   (Baustein 2) und der Durchstich-Host zeigen per `nuget.config` relativ darauf.
   *Warum:* reproduzierbar, ein Kommando, kein globaler Maschinenzustand; beide
   Geschwister-Repos sehen `../callora/artifacts/nuget-local`.

2. **Lokale Version:** `dotnet pack -p:MinVerVersionOverride=0.1.0-local`. MinVer
   erzeugt sonst pro Commit eine wandernde Höhe (`0.1.0-preview.0.<N>`), die der
   Konsument nicht stabil referenzieren kann. Feste `0.1.0-local` für den Feed.

3. **Communication-Contract kommt mit:** `Callora.Plugin.Communication.Abstractions`
   wird ebenfalls packbar. Er trägt den `ICommunicationChannelRegistry`-Typ, den
   Host und Plugins in der Default-ALC teilen müssen (Typidentität). Prod-Host und
   Plugins referenzieren dasselbe Paket → ein Typ. *Verweist auf das ALC-TODO der
   Spec; hier gelöst durch „Contract als eigenes Paket".*

4. **Lizenz-Metadaten pro Paket:** Core/Administration/Workspace =
   `AGPL-3.0-or-later`; Communication.Abstractions = `Apache-2.0` (SDK/Contract).
   Deckt die Open-Core-Entscheidung (AGPL-Core + Apache-SDK) schon im Paket ab.

5. **Analyzer nicht als Feed-Dependency:** die Module referenzieren
   `Callora.Analyzers` mit `ReferenceOutputAssembly="false"` → `pack` erzeugt keine
   Paketabhängigkeit darauf. Der konsumierende Host ist kein Framework-Assembly und
   braucht den Governance-Analyzer nicht. Bleibt so.

## Arbeitsschritte

### A. Paketierbarkeit + Metadaten (callora-Repo)
- `Directory.Build.props`: gemeinsame Repo-Metadaten für die packbaren Module
  (`PackageProjectUrl`, `RepositoryUrl`). Symbol-Pakete (snupkg) bewusst noch
  nicht — YAGNI für den Feed-Durchstich, ggf. beim CI-Publishing (Nicht-Ziel).
  `IsPackable` bleibt **projektweise** (Default false), damit Tests/CLI/Analyzers
  nicht mitpacken.
- Core/Administration/Workspace-csproj: `<IsPackable>true</IsPackable>`,
  `<Description>`, `<PackageLicenseExpression>AGPL-3.0-or-later</…>`.
  `PackageId` bleibt implizit (= Assembly-Name), das ist der gewünschte Wert.
- Communication.Abstractions-csproj: `<IsPackable>true</IsPackable>`,
  `<PackageLicenseExpression>Apache-2.0</…>`, Description.

### B. Administration-Paket: SPA rein, Quelle raus, NU5119 weg
- `src/Administration/wwwroot/.gitignore` (Inhalt `admin/`) entfernen; das Ignore
  der gebauten Vite-Assets in die Repo-Root-`.gitignore` verschieben
  (`src/Administration/wwwroot/admin/`). *Grund:* die `wwwroot/.gitignore` bricht
  `pack` mit NU5119 (Spike-Punkt 2).
- Die Frontend-**Quelle** `Resources/app/administration/**` aus dem Paket
  ausschließen (`<Content Remove>` / pack-Exclude), sodass nur die gebauten SWA
  unter `staticwebassets/admin/` landen (Spike-Punkt 3).

### C. Lokaler Feed
- `artifacts/` in Repo-`.gitignore`.
- `scripts/pack-local.sh`: räumt `artifacts/nuget-local/`, packt die vier Module mit
  `-c Release -p:MinVerVersionOverride=0.1.0-local -o artifacts/nuget-local`.

### D. Durchstich (Beweis)
- Wegwerf-Host `ops/spikes/nuget-consume/` mit eigener `nuget.config`
  (`<add key="callora-local" value="../../artifacts/nuget-local" />` + nuget.org),
  `PackageReference` auf die vier Pakete `Version=0.1.0-local`, minimaler
  `Program.cs` (`AddCalloraHost/AddCalloraAdministration` + Map*).
- Verifikation: `dotnet build` des Hosts zieht **nur** aus dem Feed (kein
  ProjectReference); Smoke via `WebApplicationFactory`/`curl` gegen `/admin`
  liefert die `index.html`. Der Ordner ist Beweis, kein Dauerartefakt.

## Durchstich-Kriterium (Erfolgskriterium 1–3 der Spec, Teil davon)
`scripts/pack-local.sh` erzeugt 4 valide `.nupkg`; der Wegwerf-Host baut allein
gegen den Feed; `GET /admin` liefert die SPA-`index.html`. Build 0/0, Suite grün.

## Risiken
- **npm im pack:** Administration packt via `BuildAdminFrontend`-Target → braucht
  Node. Für den lokalen Dev-Feed ok; CI-Publishing ist Nicht-Ziel.
- **CPM + pack:** zentrale Versionen müssen als Dependency-Versionen korrekt in die
  nuspec wandern — verifizieren durch nupkg-Inspektion.
- **SWA-Props-Konsum:** dass `staticwebassets/…StaticWebAssets.props` beim Konsum
  greift, ist der eigentliche Beweis (Spike bestätigt, hier real).
