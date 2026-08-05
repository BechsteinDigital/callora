# Code Structure Rules

Diese Datei definiert verbindliche Strukturregeln fuer Typen und Dateien.
Sie ergaenzt `ENGINEERING_RULES.md` und ist vor jeder Aufgabe zu lesen.

## Pflichtregeln

1. Keine verschachtelten Typen:
   - keine `class` in `class`
   - keine `interface` in `class`
   - keine `record` in `class`
   - keine `enum` in `class`
2. Jeder Typ in eigener Datei:
   - `public`, `internal`, `private` verschachtelte Hilfstypen sind nicht erlaubt.
   - Hilfstypen werden als top-level Typen in eigene Dateien ausgelagert.
3. Dateiname entspricht Typname:
   - Beispiel: `PluginRegistryJsonDto` -> `PluginRegistryJsonDto.cs`
4. Keine Ausnahmen ohne explizite Override-Entscheidung.

## Verbindliche Projekt-/Ordnerstruktur (Host und Plugins)

Der Host gibt die Struktur vor, jedes Plugin spiegelt sie eins zu eins. Diese
Struktur ist verbindlich fuer den Host und fuer alle Plugin-Entwicklungen.

```
<Root>/                       # Host: src/Core/   Plugin: custom/plugins/<Name>/src/
├── Domain/                   # Kern-Geschaeftslogik, keine Framework-Abhaengigkeiten
│   └── <Feature>/            # nach Feature gruppiert (z.B. Accounts/, Calls/)
├── Application/              # Use-Cases, Ports (Interfaces), Orchestrierung
│   └── <Feature>/            #   z.B. Accounts/, Calls/, Events/, Flows/
├── Infrastructure/           # Adapter: Ports auf konkrete Technik
│   ├── Persistence/
│   │   ├── <Context>DbContext.cs
│   │   ├── Entities/         # persistente Entities (falls von Domain getrennt)
│   │   ├── Configurations/   # IEntityTypeConfiguration<T>, eine Datei je Entity
│   │   └── Migrations/
│   ├── <Adapter>/            # weitere Technik-Adapter, z.B. Sip/, Audio/, Security/
├── Api/
│   ├── Workspace/            # workspace-scoped Controller (Mandanten-Ebene)
│   │   └── <Feature>/        #   Controller + Request/Response-DTOs je Feature
│   └── Admin/                # platform-/operator-scoped Controller
│       └── <Feature>/
└── <Name>Plugin.cs           # Composition Root (Host: Program.cs)
```

### Schichtregeln (Abhaengigkeitsrichtung)

1. `Domain` haengt von nichts ab — keine `Application`/`Infrastructure`/`Api`,
   keine Frameworks (EF, ASP.NET, ...).
2. `Application` haengt nur von `Domain`. Definiert Ports als Interfaces, kennt
   keine konkrete Technik.
3. `Infrastructure` haengt von `Application` + `Domain` und implementiert die Ports.
4. `Api` haengt von `Application` (+ `Domain` fuer Typen). Controller bleiben
   duenn und delegieren an `Application`.
5. Verdrahtung (Port -> Adapter) ausschliesslich im Composition Root.

### Api-Konvention

- Controller, keine Minimal-API-Endpoints. Host und Plugin einheitlich.
  Der Bestand widerspricht dieser Regel noch an vielen Stellen. Er ist in
  `ArchitectureRulesTests.MinimalApiBaseline` erfasst und eingefroren: neue
  Verstoesse scheitern im Test, und wer eine Datei auf Controller umstellt,
  streicht ihren Eintrag. Ein Eintrag ohne Verstoss laesst den Test ebenfalls
  scheitern — die Liste kann also nur schrumpfen.
- Oberste Api-Ebene trennt `Workspace/` (mandanten-scoped) von `Admin/`
  (operator-scoped).
- Request-/Response-DTOs sind eigene top-level Typen (siehe Pflichtregeln),
  je Feature gruppiert.

### Persistence-Konvention

- Ein `DbContext` je bounded context.
- `IEntityTypeConfiguration<T>` einzeln unter `Configurations/`.
- Migrationen unter `Migrations/`.

## Ziel

- Bessere Lesbarkeit und Reviewbarkeit
- Stabilere Git-Diffs
- Weniger Kopplung in grossen Klassen
- Ein Standard fuer Host und Plugins: identische Navigation ueberall
