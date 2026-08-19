# Repository Map

Erzeugt von `scripts/build-repo-map.sh`; CI prüft, dass sie aktuell ist.
Bedeutung ist kuratiert; Struktur und Größe kommen aus dem, was Git verfolgt —
nicht aus dem Dateisystem, damit die Karte auf jeder Maschine dieselbe ist.

| Pfad | Dateien | Wofür |
|---|---:|---|
| `src/Core/Domain` | 50 | Entitäten und Domänenregeln. Hängt von nichts ab — kein EF, kein ASP.NET. |
| `src/Core/Application` | 599 | Use-Cases und Ports (Interfaces). Das Herz: Plugin-Laufzeit, Surfaces, Jobs, Flows, Sicherheit. |
| `src/Core/Infrastructure` | 276 | Adapter auf konkrete Technik: EF/Postgres, Data Protection, HTTP, MCP, Startup-Dienste. |
| `src/Core/Api` | 12 | Anmeldung und Token — der einzige anonyme Endpunkt-Satz des Kerns. |
| `src/Core/Extensibility` | 5 | Die Marker-Attribute, auf denen CAL0001–0004 arbeiten. Klein und folgenreich. |
| `src/Administration/Api` | 106 | Operator-API (/api/*): Plugins, Nutzer, Rollen, Workspaces, Surfaces, Themes. |
| `src/Administration/Resources/app/administration` | 243 | Admin-SPA (Vue 3, colocated) UND das npm-Paket @callora/admin, gegen das Plugins bauen. |
| `src/Workspace` | 7 | Öffentliche Workspace-Routen und Theme-Auslieferung. |
| `src/Surface.Rendering` | 102 | Server-Rendering der Flächen: Nunjucks auf Jint in einer gehärteten Sandbox. |
| `src/Surface.Rendering/Resources/app/surface` | 51 | Surface-Laufzeit im Browser und das npm-Paket @callora/surface. |
| `src/Surface.Rendering/Resources/views` | 16 | Die mitgelieferten Nunjucks-Templates (base, layout, section, page). |
| `src/Analyzers` | 10 | Roslyn-Analyzer CAL0001–0004. Bewachen die Vertragsgrenze zur Bauzeit. |
| `src/Plugin.Sdk` | 4 | Paket ohne Code: Vertragsfläche + Analyzer + Build-Regeln in einer Referenz. |
| `src/Host/Cli` | 25 | Die callora-CLI: plugin new, test-contract, sign. |
| `src/Host/Dev` | 2 | Die einzige lauffähige Zusammenstellung im Repo. Kein Produkt — das liegt in callora-production. |
| `custom/plugins` | 2 | Installationsziel für dynamische Plugins. Im Repository absichtlich leer. |
| `custom/static-plugins` | 2 | Leer. Communication und Composer sind in eigene Repositories ausgezogen (ADR-020). |
| `tests/Callora.Core.Tests` | 360 | Die Hauptsuite. Enthält auch die Architektur- und Dokumentations-Gates. |
| `tests/Callora.Analyzers.Tests` | 5 | Analyzer-Tests: prüfen, dass CAL0001–0004 zubeißen und wo sie es nicht dürfen. |
| `tests/TestPlugins` | 8 | Minimal-Plugins, gegen die die Laufzeit getestet wird (Export, eigener DbContext). |
| `docs/adr` | 19 | Architekturentscheidungen. Bei Konflikt mit einem Issue führt das Issue. |
| `docs-site` | 76 | Die konzeptuelle Dokumentation (VitePress, Diátaxis). |
| `ops` | 4 | Betrieb: Runbooks, Frontdoor-Konfiguration, npm-Ausnahmen. |
| `scripts` | 12 | Build-, Prüf- und Release-Automatisierung. |
| `.github/workflows` | 4 | CI (Build, Integration, Frontends, Golden Path), Docs, Release, npm-Publish. |
| `.config` | 1 | Das dotnet-tools-Manifest: die gepinnten lokalen Werkzeuge (docfx, CycloneDX). |
| `docfx` | 4 | Konfiguration der generierten .NET-API-Referenz, die unter /api/ neben der docs-site liegt. |
