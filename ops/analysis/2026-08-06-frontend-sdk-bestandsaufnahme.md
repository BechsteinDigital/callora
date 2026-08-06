# Frontend- und SDK-Bestandsaufnahme — wo wir stehen

**Datum:** 2026-08-06
**Geprüfter Stand:** `a95adff` (= `origin/main`, identisch in Hauptcheckout und Worktree)
**Nicht enthalten:** die uncommitteten Communication/DTMF-Änderungen im Hauptcheckout und die
Audit-Phasen 2/3, die als PR-Entwürfe unter `ops/issues/2026-08-05-pr-audit-remediation*.md`
vorliegen — beide sind noch nicht auf `main`.

Zweck: eine belastbare Ausgangslage für die SDK-Familie
([Design](../specs/2026-08-06-admin-sdk-und-surface-composer-design.md),
[Plan](../plans/2026-08-06-sdk-familie-bausteine-1-3.md)) — was existiert, was driftet, was fehlt.

---

## 1. Kurzfassung

Die Surface-Seite hat eine Vertragsschicht und eine gepflegte Runtime; die Admin-Seite hat eine
reiche Extension-Fläche, aber keinen Vertrag. Beide leiden am selben strukturellen Problem:
**Verträge werden von Hand doppelt gepflegt, und nichts erzwingt ihre Übereinstimmung.**

Drei Drifts, alle derselben Bauart:

| Drift | Zwischen | Bemerkt durch |
|---|---|---|
| **D1** | Surface-SDK-Typen ↔ Runtime-Typen | nichts |
| **D2** | Admin-Loader ↔ Workspace-Plugin-Zuweisungen (seit `a321d34`, 31.07.) | nichts |
| **D3** | Server-`ViewId` ↔ Client-`registerSurfaceView`-Id | nichts |
| **D4** | zwei Namen für dieselbe Token-Rolle (Admin ↔ Surface) | nichts |

Dazu kommt ein Zustandsbefund, der unabhängig davon zu erledigen ist: alle drei betroffenen
npm-Pakete tragen je zwei kritische Advisories.

---

## 2. Surface — was existiert

### 2.1 `@callora/surface-sdk` (`custom/surface-sdk/`)

Version 0.1.0, Apache-2.0, `publishConfig.access: public`. Vier Quelldateien:
`index.ts`, `vite-preset.ts` und je eine Spec.

**Was der Vertrag abdeckt:**

| Bereich | Exporte |
|---|---|
| Kontextmodell | `SurfaceContext`, `SurfaceSubject`, `SurfaceCaller` (diskriminierte Union guest/authenticated) |
| Views | `SurfaceView`, `SurfaceViewParams`, `registerSurfaceView()` |
| Kontext-Kanal | `SurfaceContextChannel`, `SurfaceContextDescriptor`, `SurfaceContextPublisher`, `SurfaceContextCardinality`, `SurfaceContextKeyDiagnostics`, `surfaceContextChannel()`, `createSurfaceContextScope()` |
| Registry | `SurfaceRegistry` |
| Build | `calloraSurfacePlugin()` — IIFE, Vue external → `CalloraVue`, feste Dateinamen, Ausgabe nach `Resources/public/<surface>` |

Die Fehlertoleranz ist konsequent: `registerSurfaceView` und `surfaceContextChannel` warnen und
geben auf, statt zu werfen — „a plugin must never break the shell it is a guest in".
`createSurfaceContextScope` gibt Publisher und Abos in einem Zug zurück, was den häufigsten
Lifecycle-Fehler ausschließt.

### 2.2 Runtime (`src/Surface.Rendering/Resources/app/surface/`)

`main.ts` setzt `window.CalloraVue`, erzeugt die Registry, mountet, lädt dann die Bundles.
`mount.ts` beherrscht beide Modi (App-Root und Islands) mit reaktiver Nachregistrierung.
`plugin-loader.ts` ist der ausgereifteste Teil des gesamten Frontends: UI-Chain-Abfrage mit
`surfaceKey`-Gate, Manifest-Filterung, `contentHash`-Cache-Busting, Pfad-Sicherheitsprüfung,
Chain-Reihenfolge über `async = false`, Ladetelemetrie mit Dauer auf
`window.__calloraSurfaceLoad` plus DOM-Event, und injizierbare Abhängigkeiten für Tests.

### 2.3 SSR und Komposition

`NunjucksSurfaceRenderer` auf Jint, ohne CLR-Zugriff, frische Engine je Render, JSON-only-Kontext,
DoS-Grenzen (2 s, 32 MB, Rekursion 64, 2 Mio. Statements, 512 KB Ausgabe). Komposition über
Nunjucks-Globals `callora_slot()`/`callora_view()`/`callora_has_slot()`/`callora_navigation()`,
gespeist aus `SurfaceSlotResolver` — claim- und verfügbarkeitsgefiltert **vor** dem Markup.

### 2.4 D1 — SDK und Runtime pflegen dieselben Typen doppelt

Jeder öffentliche Typ existiert zweimal:

```
SurfaceContext            SDK: 1   Runtime: 2
SurfaceSubject            SDK: 1   Runtime: 1
SurfaceCaller             SDK: 1   Runtime: 1
SurfaceViewParams         SDK: 1   Runtime: 2
SurfaceView               SDK: 1   Runtime: 1
SurfaceContextDescriptor  SDK: 1   Runtime: 1
SurfaceContextChannel     SDK: 1   Runtime: 3
SurfaceRegistry           SDK: 1   Runtime: 3
```

Der SDK-Kommentar benennt es offen („These types mirror the surface runtime's registry"), aber
**es gibt keinen Test und keine Abhängigkeit, die sie aneinander bindet.** Der einzige Bezug im
ganzen Repo ist ein Kommentar in `golden-path.spec.ts`. Ändert jemand die Runtime, veraltet der
SDK stillschweigend — und ein Plugin kompiliert gegen einen Vertrag, den die Runtime nicht mehr
erfüllt.

**Entschieden:** Die Runtime **importiert** den Vertrag künftig aus dem SDK, statt ihn zu
spiegeln (Plan Task 18). Damit ist der Drift nicht bemerkbar, sondern unmöglich — ein Drift-Test
wird überflüssig. Möglich ist das, weil es keine externen Plugin-Autoren gibt, deren Erwartungen
geschont werden müssten; dieselbe Freiheit erlaubt, das alte `window.CalloraVue` zu **ersetzen**
statt zu aliasieren.

---

## 3. Administration — was existiert

### 3.1 Extension-Fläche

Vollständiger als erwartet: **29 `<ExtensionSlot>`**, **~50 Hook-Punkte** mit mutabler Payload und
Abbruch (`runHook('…before-…')`), **20 `useService`-Override-Punkte** mit Priority und
`getServiceConflicts()`, servergefilterte Plugin-Navigation, `extension.page.<pluginId>` als
Vollseiten-Slot.

Der Loader attributiert Registrierungen dem ladenden Plugin und meldet Ladefehler
(`getPluginUiLoadResults`) — mit einer dokumentierten Grenze: nur synchrone Registrierung auf
Bundle-Top-Level wird zugeordnet.

### 3.2 Komponenten

22 `Cal*`-Primitive, typisierte Varianten (`variant`, `tone`, `size`, `padding`), durchgehend
`--cal-*`, kein Hex im Bauteil. `tokens.scss` dokumentiert die Token-Namen ausdrücklich als
öffentlichen Vertrag, weil Plugin-Bundles nicht gegen das SCSS kompilieren.

### 3.3 Was fehlt

- **kein npm-Paket, kein Preset, keine Typen** — Communication tippt `CalloraAdminGlobal` von Hand
  ab und dupliziert 28 Zeilen Vite-Konfiguration
- **kein Zugriff auf die Primitive** von außen (`@/core/ui/` ist intern)
- **kein `apiFetch`, kein Toast/Confirm, kein Workspace-Kontext** für Plugins
- **Slot-, Hook- und Service-Namen sind lose Strings** ohne Katalog und ohne Compiler-Hilfe
- **keine eigene Route pro Plugin** (nur `/extensions/:pluginId`)
- **kein Realtime** — `CallDialer.vue` baut seinen WebSocket selbst
- **Muster-Ebene fehlt** — 15 ListViews bauen dieselbe Anordnung von Hand

### 3.4 D2 — der Admin-Loader hängt hinter der Plattform zurück

Änderungszeitpunkte:

```
loader.ts (Admin)              2026-07-27  c8300e6
plugin-loader.ts (Surface)     2026-07-29  96a3e9b
WorkspaceUiChainResolver.cs    2026-07-31  a321d34
PluginUiAssetPublisher.cs      2026-07-31  a321d34
```

Commit `a321d34` („wire workspace plugin assignments and runtime surfaces") brachte
`WorkspacePluginAssignmentService`, `WorkspacePluginsController`, `WorkspacePlugins.vue` und einen
um 55 Zeilen erweiterten `WorkspaceUiChainResolver`. Der Surface-Loader nutzt das über
`/workspace/public/ui-chain`. **Der Admin-Loader wurde nicht mitgeführt.**

| | Surface-Loader | Admin-Loader |
|---|---|---|
| Workspace-Chain | serverseitig gefiltert | **fehlt** — lädt jedes `surface === 'admin'`-Bundle |
| Cache-Busting | `contentHash` → `?v=` | fehlt |
| Pfad-Sicherheit | `isSafeRelativePath`: kein `:`, kein führender `/`, kein `..` | nur `normalizeEntryPath` |
| Ladereihenfolge | `async = false` (Chain-Ordnung) | `async = true`, nur sequenzielles `await` |
| Ladetelemetrie | Status, Dauer, DOM-Event | nur Status |
| Testbarkeit | injizierbare `fetchJson`/`loadScript`/`now` | keine |

**Der gewichtige Punkt ist der erste:** die Admin-UI eines Plugins erscheint auch dann, wenn das
Plugin dem aktiven Workspace nicht zugewiesen ist. Kein Datenleck — die APIs dahinter sind
serverseitig gegated (`PluginAdminWorkspaceResolver`, verschärft durch Audit-Finding #109) —, aber
die Zuweisung ist damit auf der UI-Ebene wirkungslos.

Die fehlende Pfadprüfung ist die zweite Auffälligkeit: das Manifest ist serverseitig erzeugt und
insofern vertrauenswürdig, aber der Surface-Loader hält dieselbe Prüfung trotzdem als
Verteidigung in der Tiefe vor.

---

## 3.5 D4 — zwei Namen für dieselbe semantische Rolle

Gefunden beim Umzug der Tokens in den Kern (Task 2).

**Was kein Befund ist:** dass die Flächen unterschiedliche Tokens führen. Ein Admin hat eine
Sidebar und eine Topbar, eine Surface nicht; eine Surface hat Sektionsraster, die der Admin nicht
kennt. Verschiedene Layouts brauchen verschiedene Maße — bei Shopware ist die Trennung zwischen
Administration und Storefront aus demselben Grund da. Ein Token, das auf der anderen Fläche nie
greift, wäre dort kein Vertrag, sondern Rauschen.

**Was der Befund ist:** dieselbe semantische Rolle trägt zwei Namen.

| Rolle | Admin | Surface |
|---|---|---|
| Vordergrund | `--cal-text` | `--cal-color-fg` |
| Schrift | `--cal-font` | `--cal-font-sans` |
| Hintergrund | `--cal-color-bg` | `--cal-color-bg` ✓ |
| gedämpft | `--cal-text-muted` | `--cal-color-muted` |
| Abstand | `--cal-space-4` | `--cal-space-4` ✓ |

Ein Block mit `var(--cal-text)` bleibt auf der Surface ungestylt, einer mit `var(--cal-color-fg)`
im Admin. Genau die Portabilität, die den Kern rechtfertigt, scheitert an den Namen — nicht an
den Werten, die sollen sich unterscheiden.

**Auflösung.** Der Kern trägt die **rollenbasierten** Tokens (Farbe, Raum, Typografie, Form,
Tiefe, Bewegung, Ebene) — 94 Stück. Flächeneigenes Chrome bleibt bei der Fläche: die drei
Admin-Maße stehen seit Task 2 in `core/design/layout.scss`, und die Surface darf ihre eigenen
führen. Beim Umzug der Runtime (Task 18) werden `--cal-color-fg` und `--cal-font-sans` auf die
Rollennamen des Kerns abgebildet; betroffen sind dann auch der Themes-Guide und die Surface-Demo.

---

## 4. D3 — Server- und Client-Registrierung einer View

`HostSurfaceViewRegistration.ViewId` (C#) und `registerSurfaceView({ id })` (TS) müssen
übereinstimmen; verbunden sind sie durch nichts als eine Zeichenkette. Ein Tippfehler erzeugt eine
Insel, die nie gefüllt wird — stumm, und erst in Produktion sichtbar.

Behoben durch Task 19 des Plans (Konsistenztest nach dem Muster der Doku-Prüfung aus `69d3195`).

---

## 5. Audit-Kontext (#123)

Ein repositoryweiter Audit vom 2026-08-05 hat 21 Findings erzeugt. Stand:

| Phase | Findings | Stand |
|---|---|---|
| Release-Gate | #102, #103 | erledigt |
| 1 — Sicherheitsgrenzen | #104–#109 | im PR-Entwurf `pr-audit-remediation.md`, nicht auf `main` |
| 2 — Communication-Runtime | #110–#113, #115, #117 | teils im Entwurf, teils uncommitted im Hauptcheckout |
| 3 — Produktverdrahtung | #114, #116 | im Entwurf `-phase2.md`, nicht auf `main` |
| 4 — Lieferkette und Verträge | #118–#122 | #119 und #122 auf `main`; **#118, #120, #121 offen** |

Zwei offene Findings betreffen diese Arbeit unmittelbar:

**#121 (Frontend-CI und Lieferkette)** verlangt für *jedes aktive npm-Lockfile* Install-, Build-,
Test- und **Audit**-Gates, Dependabot-Abdeckung aller Workspaces und die Behebung verwundbarer
Abhängigkeiten. Das deckt sich mit Task 1 des Plans, geht aber darüber hinaus: mein CI-Job hat
kein `npm audit` und keine Dependabot-Einträge.

**#118 (Marketplace-Isolationsgrenze)** hält fest, dass Plugin-Assemblies und Admin-JavaScript
bewusst privilegierter Host-Code sind und Signaturen die Laufzeit nicht einschränken — tragfähig
nur unter einem vollständig vertrauenden Publisher-Modell. Das deckt sich exakt mit §5.5 P4 des
Composer-Designs („im Browser gibt es keine Plugin-Isolation, und das wird nicht behauptet") und
bestätigt diese Einordnung von unabhängiger Seite.

---

## 6. npm-Verwundbarkeiten

`npm audit --package-lock-only` gegen den geprüften Stand:

| Paket | Befund |
|---|---|
| `src/Administration/Resources/app/administration` | 6 Advisories — 2 kritisch, 1 hoch, 3 mittel (`happy-dom`) |
| `src/Surface.Rendering/Resources/app/surface` | 8 Advisories — 2 kritisch, 2 hoch, 4 mittel (`postcss` u. a.) |
| `custom/surface-sdk` | 7 Advisories — 2 kritisch, 1 hoch, 4 mittel (`postcss` u. a.) |

Bei Surface-Runtime und SDK ist die Behebung laut `npm audit fix` ohne Breaking Change möglich; in
der Admin-Shell erfordert `happy-dom@20` einen Sprung. `happy-dom` ist reine Test-Abhängigkeit —
die beiden kritischen Advisories betreffen also nicht das ausgelieferte Bundle, wohl aber die
Testumgebung, die untrusted Markup verarbeitet.

Relevanz für den Plan: `@callora/ui-core` und `@callora/admin-sdk` erben diese Abhängigkeiten
beim Anlegen, wenn nicht gleich auf saubere Stände gesetzt wird.

---

## 7. Was noch zu tun ist

### 7.1 Vor der SDK-Arbeit

| # | Aufgabe | Warum jetzt |
|---|---|---|
| V1 | **Admin-Loader an den Surface-Loader angleichen** (Chain, Cache-Busting, Pfadprüfung, Reihenfolge, Telemetrie, injizierbare Deps) | Task 15 des Plans fasst `loader.ts` an und würde den Drift sonst zementieren; die Workspace-Zuweisung ist unabhängig davon ein Fehlverhalten |
| V2 | **npm-Advisories der drei Pakete beheben** | die neuen Pakete erben sie sonst; deckt einen Teil von #121 |

### 7.2 Innerhalb der SDK-Arbeit (Plan Bausteine 1–3)

Unverändert wie geplant, mit zwei Ergänzungen aus dieser Bestandsaufnahme:

- Der CI-Job aus Task 1 bekommt **`npm audit --audit-level=high`** und die neuen Pakete werden in
  `.github/dependabot.yml` aufgenommen (#121).
- D1 wird **strukturell aufgelöst**: die Runtime importiert den Vertrag aus dem SDK (Task 18).

### 7.2a Was der fehlende Fremdkonsument sonst noch erlaubt

Es gibt keine externen Plugin-Autoren, also auch keine Kompatibilitätsschulden. Drei Kompromisse
entfallen dadurch — und sie sollten jetzt entfallen, weil jeder von ihnen mit dem ersten fremden
Bundle dauerhaft würde:

1. **Kein Alias-Global.** `window.CalloraVue` und `window.CalloraAdmin.vue` werden ersetzt, nicht
   gespiegelt. Ein Alias, den niemand braucht, ist ein zweiter Weg zum selben Ziel — und damit der
   Anfang des nächsten Drifts.
2. **Kein Drift-Test, wo ein Import genügt** (D1, siehe oben).
3. **Kein `normalizeEntryPath`-Hack.** Der Admin-Loader strippt heute `custom/plugins/` aus
   Manifestpfaden, um eine Inkonsistenz in der Pfadform auszugleichen. Beim Angleich (V1) gehört
   die Pfadform vereinheitlicht, statt den Ausgleich mitzuschleppen.

### 7.3 Reihenfolgekonflikt

**Task 16 (Communication-Bundle auf die SDK umstellen) muss warten**, bis die Audit-Phase-3-Arbeit
gemergt ist. Finding #116 bringt den Dialer als Vue-Komponente in genau dieses Bundle zurück; eine
gleichzeitige Umstellung auf die SDK erzeugt einen vermeidbaren Konflikt in denselben Dateien.

Der Plan bleibt bis Task 15 vollständig ausführbar. Als Beleg-Konsument für Baustein 2 kann
ersatzweise `SurfaceDemo` dienen oder ein neu angelegtes Minimal-Plugin.

### 7.4 Danach

Bausteine 4–7 des Composer-Designs, unverändert. Für Baustein 7 bleibt #118 der Rahmen: solange
das Trust-Modell „vollständig vertrauender Publisher" gilt, ist der Composer als in-process-Plugin
korrekt eingeordnet; ein offener Marktplatz würde erst die dort geforderte Isolationsgrenze
brauchen.

---

## 8. Bewertung

Was gebaut ist, ist gut gebaut — der Surface-Loader, der Nunjucks-Sandbox, das Slot-Modell und die
Primitive halten einem Vergleich mit Shopware stand und übertreffen es an mehreren Stellen. Die
Schwäche liegt nicht in der Qualität der Teile, sondern **in den Nähten zwischen ihnen**: drei
Verträge werden doppelt gepflegt, und keiner davon ist maschinell abgesichert.

Genau das ist der Grund, aus dem die SDK-Familie mehr ist als Bequemlichkeit für Plugin-Autoren.
Ein gemeinsamer Kern beseitigt D1 strukturell, der generierte Extension-Point-Katalog beseitigt die
lose Kopplung bei Slots und Hooks, und der Konsistenztest schließt D3. D2 ist davon unabhängig und
sollte vorher fallen.
