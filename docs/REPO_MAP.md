# Repository Map

Erzeugt von `scripts/build-repo-map.sh`; CI prüft, dass sie aktuell ist.
Welche Verzeichnisse es gibt, kommt aus dem, was Git verfolgt; wofür sie da sind,
ist kuratiert und steht in scripts/build-repo-map.sh.

| Pfad | Wofür |
|---|---|
| `src/Core/Domain` | Entitäten und Domänenregeln. Hängt von nichts ab — kein EF, kein ASP.NET. |
| `src/Core/Application` | Use-Cases und Ports (Interfaces). Das Herz: Plugin-Laufzeit, Surfaces, Jobs, Flows, Sicherheit. |
| `src/Core/Infrastructure` | Adapter auf konkrete Technik: EF/Postgres, Data Protection, HTTP, MCP, Startup-Dienste. |
| `src/Core/Api` | Anmeldung und Token — der einzige anonyme Endpunkt-Satz des Kerns. |
| `src/Core/Extensibility` | Die Marker-Attribute, auf denen CAL0001–0004 arbeiten. Klein und folgenreich. |
| `src/Administration/Api` | Operator-API (/api/*): Plugins, Nutzer, Rollen, Workspaces, Surfaces, Themes. |
| `src/Administration/Resources/app/administration` | Admin-SPA (Vue 3, colocated) UND das npm-Paket @callora/admin, gegen das Plugins bauen. |
| `src/Workspace` | Öffentliche Workspace-Routen und Theme-Auslieferung. |
| `src/Surface.Rendering` | Server-Rendering der Flächen: Nunjucks auf Jint in einer gehärteten Sandbox. |
| `src/Surface.Rendering/Resources/app/surface` | Surface-Laufzeit im Browser und das npm-Paket @callora/surface. |
| `src/Surface.Rendering/Resources/views` | Die mitgelieferten Nunjucks-Templates (base, layout, section, page). |
| `src/Analyzers` | Roslyn-Analyzer CAL0001–0004. Bewachen die Vertragsgrenze zur Bauzeit. |
| `src/Plugin.Sdk` | Paket ohne Code: Vertragsfläche + Analyzer + Build-Regeln in einer Referenz. |
| `src/Host/Cli` | Die callora-CLI: plugin new, test-contract, sign. |
| `src/Host/Dev` | Die einzige lauffähige Zusammenstellung im Repo. Kein Produkt — das liegt in callora-production. |
| `custom/plugins` | Installationsziel für dynamische Plugins. Im Repository absichtlich leer. |
| `custom/static-plugins` | Leer. Communication und Composer sind in eigene Repositories ausgezogen (ADR-020). |
| `tests/Callora.Core.Tests` | Die Hauptsuite. Enthält auch die Architektur- und Dokumentations-Gates. |
| `tests/Callora.Analyzers.Tests` | Analyzer-Tests: prüfen, dass CAL0001–0004 zubeißen und wo sie es nicht dürfen. |
| `tests/TestPlugins` | Minimal-Plugins, gegen die die Laufzeit getestet wird (Export, eigener DbContext). |
| `docs/adr` | Architekturentscheidungen. Bei Konflikt mit einem Issue führt das Issue. |
| `docs-site` | Die konzeptuelle Dokumentation (VitePress, Diátaxis). |
| `ops` | Betrieb: Runbooks, Frontdoor-Konfiguration, npm-Ausnahmen. |
| `scripts` | Build-, Prüf- und Release-Automatisierung. |
| `.github/workflows` | CI (Build, Integration, Frontends, Golden Path), Docs, Release, npm-Publish. |
| `.config` | Das dotnet-tools-Manifest: die gepinnten lokalen Werkzeuge (docfx, CycloneDX). |
| `docfx` | Konfiguration der generierten .NET-API-Referenz, die unter /api/ neben der docs-site liegt. |
