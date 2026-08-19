<div align="center">

# Callora

**Die offene Plattform für Kommunikationsprodukte.**

Ein domänenneutraler .NET-Kern, ein echtes Plugin-Modell und ein visueller Editor, mit dem
Arbeitsplätze und Portale gebaut werden — nicht programmiert.

[Dokumentation](docs-site/) · [Architektur](docs/adr/) · [Erweiterungspunkte](docs-site/developers/extension-points.md)

</div>

---

## Was Callora ist

Eine Plattform, kein Produkt. Der Kern weiß nichts von Telefonie, Terminen oder Kunden — er
weiß, wie **Plugins** geladen, isoliert, versorgt und ausgeliefert werden. Alles Fachliche kommt
aus Plugins, auch das, was wir selbst mitliefern.

Das ist dieselbe Wette wie bei Shopware oder Odoo, nur für einen anderen Markt: Wer ein
Contact Center, ein Kundenportal oder einen Agenten-Arbeitsplatz braucht, soll ihn
**zusammensetzen** statt ihn bauen zu lassen.

### Die drei Ebenen, die das tragen

```
Workspace          — die Daten (ein Mandant, ein Datenbestand)
 └─ Surface        — der Zugang (Domain, Anmeldung, Design)
     └─ Surface    — die Struktur (Seiten, beliebig tief, vom Kunden gebaut)
         └─ Layout — was der Composer daraus macht
```

Ein Workspace kann mehrere Zugänge auf denselben Daten haben — eine öffentliche Website, ein
Agenten-Desktop, ein Dialer. Jeder davon ist ein Baum aus Seiten, und jede Seite kann eine
Erlebniswelt tragen ([ADR-019](docs/adr/ADR-019-surfaces-als-baum.md)).

## Was es besonders macht

**Plugins laufen im Prozess, nicht daneben.** Jedes bekommt seinen eigenen
`AssemblyLoadContext` und sein eigenes Datenbankschema (`plugin_<id>`), teilt aber die
Typidentität mit dem Host. Ein Plugin exportiert Verträge, die andere Plugins konsumieren —
ohne HTTP dazwischen ([ADR-013](docs/adr/ADR-013-trust-model-trusted-in-process.md)).

**Die Vertragsfläche wird vom Compiler bewacht.** `[CalloraInternal]`, CAL0001–0003 und
PublicApiAnalyzers sorgen dafür, dass „öffentliche API" keine Absichtserklärung ist: Wer die
Grenze überschreitet, sieht es beim Bauen, nicht beim Kunden.

**Der Editor rendert die echten Komponenten.** Kein iframe, kein zweiter Renderpfad, keine
Vorschau, die driftet: Der Canvas lädt dieselben Vue-Komponenten und dasselbe Stylesheet wie die
Fläche, nur gescoped. Was im Editor steht, steht auch live.

**Gestaltung hat Leitplanken.** Das Konfigurationspanel eines Blocks wird aus seinem Vertrag
generiert, und die Erscheinungs-Controls wählen aus `--cal-*`-Rollen — kein freier Farbwähler,
keine Pixelfelder. Eine zusammengesetzte Seite sieht deshalb weiterhin nach dem Produkt aus.

**Kontext überquert Flächengrenzen.** Ein Anruf, den der Agenten-Desktop annimmt, ist derselbe,
den das Kundenportal sieht — über einen deklarierten, feldweise sichtbaren Kanal
([ADR-017](docs/adr/ADR-017-surface-identitaet-und-session-transport.md)).

## Schnellstart

Nichts installiert außer Docker? Dann dieser Weg — er baut Host, beide
Oberflächen und jedes geklonte Plugin im Image:

```bash
git clone https://github.com/BechsteinDigital/callora.git
cd callora

# Plugins, die dabei sein sollen — optional, der Host läuft auch ohne
git clone <communication> custom/static-plugins/Communication
git clone <videoconference> custom/plugins/videoconference

docker compose -f docker-compose.standalone.yml up --build
```

Admin unter `http://localhost:5000/admin`. Aufgezählt wird hier nichts: Gebaut
und geladen wird, was unter `custom/` eine `registry.json` hat — dieselbe Suche
für Build und Discovery.

### Daran entwickeln

```bash
scripts/dev-build.sh                 # Host + alle geklonten Plugins
docker compose up -d                 # Stack mit dotnet watch, Postgres, TURN
```

`dotnet watch` baut den **Host** neu, nicht die Plugins. Nach einer
Plugin-Änderung `scripts/dev-build.sh --plugins <name>`.

### Einzeln bauen

Ein voller Lauf baut beide Vue-Suiten über ihre MSBuild-Targets und jedes
Plugin. Wer an einer Stelle arbeitet, braucht nur diese:

```bash
scripts/dev-build.sh --only admin        # Vue-Shell unter /admin
scripts/dev-build.sh --only surface      # Flächen-Runtime + SSR
scripts/dev-build.sh --only host         # nur .NET, ohne Node
scripts/dev-build.sh --plugins composer  # ein Plugin (C# + Bundles)
```

### Ohne Docker

```bash
dotnet restore Callora.Host.sln
dotnet build Callora.Host.sln        # baut beide Oberflächen mit (vue-tsc + vite)
dotnet test Callora.Host.sln
```

Ohne Node: `dotnet build -p:SkipAdminFrontend=true -p:SkipSurfaceFrontend=true`

Die Vitest-Suiten der Frontends laufen eigenständig:

```bash
cd src/Administration/Resources/app/administration && npm ci && npm run test
cd src/Surface.Rendering/Resources/app/surface     && npm ci && npm run test
```

## Ein Plugin bauen

```csharp
public sealed class MyPlugin : IHostManagedPlugin
{
    public string PluginId => "my-plugin";
    public string DisplayName => "Mein Plugin";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken ct = default)
    {
        // Verträge exportieren, die andere Plugins konsumieren
        context.Export<IMyContract>(new MyImplementation());
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
}
```

Dazu ein `registry.json`, optional ein eigenes Datenbankschema, eine Admin-UI als IIFE-Bundle
und Blöcke für den Editor. Der Weg dahin steht in
[Build your first Callora plugin](docs-site/guides/getting-started/) und
[Building a surface plugin](docs-site/guides/surface/building-a-surface-plugin.md).

## Aufbau des Repositories

| Pfad | Was |
|---|---|
| `src/Core` | Der domänenneutrale Kern (`Callora.Core`) |
| `src/Administration` | Admin-Modul samt colocated Vue-3-Shell |
| `src/Workspace` | Workspaces, Surfaces, öffentliches Routing |
| `src/Surface.Rendering` | Flächen-Rendering (Nunjucks-SSR) und `@callora/surface` |
| `src/Analyzers` | Roslyn-Analyzer, die die Vertragsfläche bewachen |
| `src/Plugin.Sdk` | `Callora.Plugin.Sdk` — eine Referenz, gegen die ein Plugin baut |
| `src/Host/Cli` | Die `callora`-CLI |
| `src/Host/Dev` | Die lauffähige Zusammenstellung dieses Repos — kein Produkt |
| `custom/static-plugins/*` | Mitgelieferte System-Plugins (Communication, Composer) |
| `custom/plugins/` | Installationsziel für dynamische Plugins — im Repository leer |
| `docs-site/` | Die Dokumentation (VitePress) |
| `docs/adr/` | Architekturentscheidungen |

Dieses Repository ist das **Framework** — ein Satz paketierbarer Bibliotheken. Der lauffähige
Prozess und die Zusammenstellung einer Distribution liegen im separaten Repository
`callora-production`; dasselbe Framework kann mehrere Distributionen tragen.

Die Plugins unter `custom/static-plugins` ziehen in eigene, private Repositories und werden
als Pakete bezogen; ihre **Verträge** bleiben öffentlich, damit ein Dritter dagegen bauen
kann, ohne die Implementierung zu sehen ([ADR-020](docs/adr/ADR-020-repo-schnitt-und-paketgrenzen.md)).

## Dokumentation

```bash
cd docs-site && npm ci && npm run dev
```

- **[Nutzer](docs-site/users/)** — Workspaces, Surfaces, Administration
- **[Entwickler](docs-site/developers/)** — Verträge, Erweiterungspunkte, Plugin-Bau
- **[Referenz](docs-site/reference/)** — APIs, Manifeste, Analyzer-Regeln, Berechtigungen
- **[Betrieb](docs-site/maintainer/)** — Deployment, Migrationen, Sicherheit

## Mitmachen

Fehlerberichte, Vorschläge und Pull Requests sind willkommen. Was du vorher wissen solltest,
steht in **[CONTRIBUTING.md](CONTRIBUTING.md)** — vor allem, was das Repository beim Bauen
erzwingt: API-Baselines, Governance-Analyzer und Architektur-Tests schlagen zu, bevor ein Review
es täte.

Beiträge laufen über den **Developer Certificate of Origin**: eine Zeile im Commit
(`git commit -s`), kein Vertrag, keine Rechteabtretung. Warum das reicht und warum es kein CLA
gibt, steht dort ebenfalls.

## Lizenz

Callora steht unter der **[Apache-Lizenz 2.0](LICENSE)**.

Das gilt für alles in diesem Repository, einschließlich der Pakete `@callora/surface` und
`@callora/admin`, gegen die ein Plugin kompiliert. **Ein Plugin darf beliebig lizenziert sein,
auch proprietär** — Apache-2.0 verlangt davon nichts.

Apache und nicht MIT wegen der ausdrücklichen **Patentklausel**: Bei Codecs, SIP und Echo
Cancellation ist Patentrecht real, und MIT adressiert es nicht.

© 2026 Bechstein.Digital Ecommerce UG (haftungsbeschränkt)
