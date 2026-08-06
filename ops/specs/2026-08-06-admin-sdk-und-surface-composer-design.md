# Admin-SDK, SDK-Familie und Surface Composer — Design

**Datum:** 2026-08-06
**Status:** Konzept abgestimmt (Abschnitte 1–5 freigegeben), Umsetzung in Bausteinen
**Kontext:** Die Surface-Seite hat mit `@callora/surface-sdk` eine echte Vertragsschicht, die
Administration hat keine — Plugin-Autoren tippen `window.CalloraAdmin` von Hand ab und duplizieren
die Vite-Konfiguration. Gleichzeitig fehlt beiden Flächen ein gemeinsamer Baustein-Vorrat, ein
Realtime-Zugang und die Grundlage für einen visuellen Editor, mit dem Nicht-Entwickler eigene
Surfaces bauen können.

Dieses Dokument fasst die Analyse beider Flächen, den Vergleich mit Shopware und der
Website-Builder-Klasse, die getroffenen Entscheidungen und das gemeinsame Zielbild zusammen.

**Arbeitstitel des Editors:** *Surface Composer*. Der endgültige Name ist offen (§9).

---

## 1. Analyse des Ist-Zustands

### 1.1 Surface — SDK vorhanden, aber nur für die halbe Fläche

`@callora/surface-sdk` (Apache-2.0, `custom/surface-sdk/`) liefert zwei Dinge: den Typvertrag
(`SurfaceContext`, `SurfaceView`, `SurfaceContextChannel`, `registerSurfaceView`) und das
Vite-Preset `calloraSurfacePlugin` — IIFE-Bundle, Vue external gegen `window.CalloraVue`, feste
Dateinamen `main.js`/`main.css`, Ausgabe nach `Resources/public/<surface>`.

Die Laufzeit (`src/Surface.Rendering/Resources/app/surface/`) dockt zweigleisig an:

- **App-Modus** — ein `#callora-app`-Root, `App.vue` rendert alle registrierten Views nach `order`.
- **Islands-Modus** — `[data-callora-island="<viewId>"]`-Platzhalter in SSR-Inhalt, je Platzhalter
  wird genau die passende View gemountet.

Beide sind reaktiv: `plugin-loader.ts` lädt die Bundles **nach** `mountSurface`, und weil
`registry.views` ein Vue-`reactive`-Array ist, erscheint eine spät registrierte View ohne
DOM-Rescan. Ladefehler sind fail-soft, aber nicht fail-silent (`__calloraSurfaceLoad` +
`callora:surface-load`-Event).

Die **Server-Hälfte ist deutlich reicher als die SDK**: `IHostSurfaceViewContributor` deklariert
`HostSurfaceViewRegistration(ViewId, Slot, DisplayName, Weight, Cardinality, RequiredClaims,
ProvidesContexts, RequiresContexts)`; `SurfaceSlotResolver` filtert claim- und
verfügbarkeitsbasiert **vor** dem Markup; die Nunjucks-Globals `callora_slot()` / `callora_view()`
emittieren daraus die Islands. Der XML-Kommentar an `DisplayName` sagt wörtlich *„for admin and a
later layout editor"* — das Datenmodell für den Editor ist im Vertrag bereits angelegt.

**Kontext-Verbindung:** `SurfaceContextChannel` — Keys namespaced und versioniert
(`crm.lead-selection/v1`, per Regex erzwungen), jeder Publisher deklariert sich,
`single`/`multiple`-Cardinality, optionale `validate`, Late-Subscriber erhalten sofort den
Snapshot, `diagnostics()` legt Publisher/Subscriber/Rejects offen. Bewusst kein Event-Bus.
Trägt laut Kommentar *„UI state, never authority"*.

**Lücken der Surface-SDK:**

| Lücke | Konsequenz |
|---|---|
| **SDK und Runtime deklarieren jeden Typ doppelt** | Handgepflegte Spiegelung ohne Test und ohne Abhängigkeit — die Runtime kann sich ändern, ohne dass der Vertrag es merkt (Bestandsaufnahme D1) |
| Server-Deklaration hat kein TS-Gegenstück | `ViewId` muss in C# und TS manuell übereinstimmen — nichts fängt einen Typo |
| `SurfaceView.component: Component` | Die Runtime übergibt `params`, der Vertrag typisiert das nicht |
| Kein API-/Fetch-Helfer | Jede Surface baut Session-Cookie-Handling und Fehlerform neu |
| Kein Realtime-Client | Die WS-Fläche existiert im Host (`/ws/{pluginId}/…`), die Surface-Runtime hat nichts |
| Kein Routing | Multi-Page-App-Surface nur per Eigenbau |
| Keine UI-Bausteine | Jede Surface erfindet Button, Card, Tabelle neu |
| Token-Zugriff nur als CSS-Variable | Kein JS-Zugriff auf das aufgelöste Theme |
| Kanal ist surface-gebunden | Cross-Surface nur über `SurfaceHandoffService` (Identität, nicht Anwendungszustand) — siehe §5 |

### 1.2 Administration — Extension-Punkte vorhanden, SDK fehlt

Die Extension-Fläche ist erstaunlich vollständig:

- **29 `<ExtensionSlot>`** in den Modulen (`users.list.toolbar`, `users.detail.fields`,
  `dashboard.metrics`, `config.fields`, …)
- **~50 Hook-Punkte** über `runHook('…before-…' / '…after-…')` mit **mutable Payload und
  cancelable** — das Umbraco-Modell aus der Extension-Points-Landkarte, hier bereits gelandet
- **20 `useService`-Override-Punkte** mit Priority und Konflikt-Diagnostik
  (`getServiceConflicts`)
- servergefilterte Plugin-Navigation (`/api/ext/admin/navigation`)
- `extension.page.<pluginId>` als Vollseiten-Slot unter `/extensions/:pluginId`
- `loader.ts` attributiert Registrierungen dem ladenden Plugin und meldet Ladefehler
  (`getPluginUiLoadResults`)

**Und dann bricht es an der Vertragskante ab.** `window.CalloraAdmin` ist ein untypisiertes Global.
Das Communication-Plugin tippt das Interface von Hand ab:

```ts
interface CalloraAdminGlobal { registerExtension(slot: string, component: unknown, order?: number): void }
```

und dupliziert 28 Zeilen Vite-Konfiguration. Konkret fehlt gegenüber der Surface-Seite:

- **kein npm-Paket, kein Preset, keine Typen**
- **kein Zugriff auf das Design-System** — 22 `Cal*`-Komponenten nur über `@/core/ui/` erreichbar.
  Ein Plugin bekommt ausschließlich die CSS-Custom-Properties (die sauber als öffentlicher Vertrag
  dokumentiert sind) und baut den Rest nach
- **kein `apiFetch`, kein Toast/Confirm, kein Workspace-Kontext** für Plugins
- **Slot-, Hook- und Service-Namen sind lose Strings** ohne Katalog, ohne Discovery, ohne
  Compiler-Hilfe — Lücke B/C der Extension-Points-Landkarte, für Code-Extensions adressiert, für
  UI-Extensions offen
- **keine eigene Route pro Plugin**
- **kein Realtime** — `CallDialer.vue` baut sich seinen WebSocket selbst

### 1.3 Komponenten — besser als erwartet, zwei Ebenen fehlen

Die `Cal*`-Komponenten sind sauber gebaut: typisierte Varianten (`variant`, `tone`, `size`,
`padding`), durchgehend `--cal-*`-Tokens, kein Hex im Bauteil, scoped SCSS. `tokens.scss`
dokumentiert die Token-Namen ausdrücklich als **öffentlichen Vertrag**, weil Plugin-Bundles nicht
gegen das SCSS kompilieren, sondern sich nur an Laufzeit-Variablen hängen können.

Was fehlt:

1. **Muster-Ebene.** Alle 15 ListViews bauen dieselbe Anordnung von Hand:
   `CalPage > CalPageHeader > ExtensionSlot(toolbar) > CalCard > CalDataTable + ExtensionSlot(row-actions)`.
2. **Ersetzbarkeit.** Keine Komponente ist durch ein Plugin oder Theme austauschbar.
3. ~~Flächenneutralität~~ — hinfällig seit §6.1: `CalButton` darf `vue-router` importieren, weil
   es in einem Admin-Paket lebt.

---

## 2. Vergleich

### 2.1 Shopware CMS („Erlebniswelten")

**Das Modell:** `Page → Section → Block → Slot → Element`. Tragende Trennung ist **Block ≠
Element**: der Block ist das Layout (Entwickler-Arrangement mit benannten Slots), das Element der
Inhalt darin. `text-on-image` deklariert `slots: { content: { type: 'text', default: {…} } }`.

**Section** (`CmsSectionEntity`): `type`, `sizingMode`, `mobileBehavior`, `backgroundColor`,
`backgroundMedia`, `cssClass`, `visibility`, `locked`.
**Block** (`CmsBlockEntity`): `sectionPosition`, `margin*`, `background*`, `cssClass`,
`visibility`, `locked`.

**Datenbindung** (`FieldConfig`): `source` ∈ `static | mapped | default | product_stream`.
`mapped` löst zur Request-Zeit einen Pfad im Kontext-Entity auf (`product.name`);
`AbstractCmsElementResolver.resolveEntityValue` läuft den Pfad ab (mit „smartDetect", der ein
überflüssiges Präfix toleriert), `resolveEntityValues` ersetzt zusätzlich `{{ property }}` in
statischen Texten. Der Editor bietet die gültigen Pfade an, weil `getEntityMappingTypes()` das
DAL-Schema in typisierte Pfadlisten übersetzt — inklusive Custom Fields.

**Zweiphasige Auflösung** (`CmsSlotsDataResolver`): `collect()` sammelt je Slot eine
`CriteriaCollection`, alle werden gemergt und optimiert (`optimizeCriteriaObjects`), in wenigen
Queries geholt, dann verteilt `enrich()` zurück. Zwanzig Blöcke kosten nicht zwanzig Queries.

**Registrierung im Admin** (`cms.service.ts`): ein Element deklariert `component` (Darstellung),
`configComponent` (Konfig-Panel) und `previewComponent` (Auswahlkachel) — drei Komponenten je
Element.

**Wo Shopware bricht:**

1. **Block-Kategorien sind ein geschlossenes Enum** in `cms-1.0.xsd`
   (`commerce|form|image|sidebar|text|text-image|video`). Eine App kann keine eigene Kategorie
   beitragen.
2. **Zwei Klassen von Erweiterern.** Ein Plugin registriert per JS und kann alles. Eine App liefert
   `cms.xml` und kann: keine eigene Konfig-Komponente, **keine Elemente überhaupt**, keine eigene
   Kategorie.
3. **`default-config` ist ein festes `xs:all`** (margin/sizing/background) — kein freies,
   typisiertes Konfigschema je Block.
4. **Slot-Config ist untypisiert** (`name`/`source`/`value` als Strings). Keine Validierung, keine
   UI-Ableitung, keine Migration bei Schemaänderung.
5. **Datenbindung ist rein pull-basiert** zur Request-Zeit — kein Live-Konzept.

### 2.2 Die Builder-Klasse

**Framer** (`addPropertyControls`) dreht die Verantwortung um: der Entwickler *deklariert*
typisierte Controls, der Editor **generiert das Panel**. Rund 22 Typen (String, Number, Boolean,
Color, Enum, Array, Object, ComponentInstance, ResponsiveImage, Date, File, Transition,
EventHandler, Padding, BorderRadius, Border, BoxShadow, Gap, Font, Cursor, Link) mit `title`,
`defaultValue`, `description` (Markdown), `placeholder`, `optional`, `disabled`, `min`/`max` — und
`hidden(props)` als **Funktion** für bedingte Sichtbarkeit. Kein Block schreibt Konfig-UI.

**Webflow** ergänzt drei Prop-Typen, die Shopware fehlen: `Slot` (Bereich für andere Komponenten —
als *Prop*, nicht als Sonderkonstrukt), `Variant` (vordefinierte Ausprägungen) und `Visibility`.
Props werden per `group` im Panel gruppiert. Dazu Variablen/Modes als Design-Tokens im Editor.

**Onepage** ist die relevanteste Haltung: sektionsbasiert, austauschbare Style-Presets, und
ausdrücklich *„verhindert Aktionen, die dein Layout zerstören könnten"*. Guardrails statt Freiheit —
die Antwort auf „Laien ohne großen Programmieraufwand".

### 2.3 Calloras Vorsprung

Keiner der vier hat ein Live-Daten-Konzept. Shopwares `mapped` löst zur Request-Zeit auf und friert
dann ein. Callora hat bereits den `SurfaceContextChannel` mit versionierten Keys,
Publisher-Deklaration und Late-Subscriber-Snapshot — die Infrastruktur für eine echte
Live-Bindung.

---

## 3. Entscheidungen

| # | Frage | Entscheidung |
|---|---|---|
| E1 | Reichweite des Editors | **Surfaces zuerst** (Außenwirkung), Block-Vertrag jedoch flächenneutral entworfen, damit Admin-Dashboards ohne Bruch nachziehen können |
| E2 | Konfigurationsmodell | **Deklarativ, Panel generiert** (Framer-Modell). Kein Block schreibt Konfig-UI |
| E3 | Layout-Kontrolle | **Sektionen + Token-Stellschrauben** (Onepage-Haltung). Keine freien px-/Hex-Werte; jeder Wert wählt eine `--cal-*`-Rolle oder -Stufe |
| E4 | SDK-Schnitt | **Zwei Pakete, je im Modul** (`@callora/admin`, `@callora/surface`), Apache-2.0 — revidiert am 2026-08-06, siehe §6.1 |
| E5 | Editor ↔ Rendering | **Kompositions-Renderer neben Nunjucks**; Layout als Daten, Ausgabe als Islands |
| E6 | Editor-Canvas | **Der Editor ist die Live-Vorschau** — kein iframe, kein zweiter Renderpfad |
| E7 | Cross-Surface-Kontext | **Wird gelöst** — zweiter, serverseitiger Kanal mit Ankern (§5) |

**Zu E5, hartes Ausschlusskriterium:** Plugin-Pakete sind signaturgeprüft
(`ManifestSignaturePluginPackageVerifier`), und `PluginUiAssetPublisher` publiziert
`/plugin-assets` bei jeder Aktivierung neu aus dem Paket (über ein Staging-Verzeichnis). Ein
Editor kann daher **keine Templates in Bundles schreiben** — sie wären weder signiert noch
überlebten sie die nächste Aktivierung. Damit scheidet „Editor kompiliert nach Nunjucks" aus.

---

## 4. Der gemeinsame Vertrag

Bezeichner in Englisch, wie im übrigen Repo.

### 4.1 Bindungsarten

```ts
type Binding<T> =
  | { source: 'static';  value: T }
  | { source: 'context'; key: string; scope?: 'local' | 'shared'; path?: string }
  | { source: 'inherit' }
  | { source: 'default' }
```

`context` ist der Punkt, an dem Callora die Vorbilder überholt: der Wert kommt nicht aus einer
eingefrorenen Request-Zeit-Auflösung, sondern aus einem versionierten Kontext-Key. Ein Block,
dessen `call`-Control an `communication.active-call/v1` gebunden ist, aktualisiert sich beim
eingehenden Anruf, **ohne dass jemand Realtime-Code schreibt**.

`scope` ist optional und normalerweise nicht anzugeben — der Auflöser entscheidet (§5.3).

### 4.2 Control-Typen

Kalibriert an Framer, **gefiltert auf das, was mit Token-Guardrails verträglich ist**:

| Gruppe | Typen |
|---|---|
| Inhalt | `text`, `richText`, `number`, `toggle`, `select`, `list`, `group`, `media`, `link`, `date` |
| Bindung | `context` (Kontext-Key), `query` (serverseitige Datenquelle) |
| Gestalt | `colorToken`, `spacingToken`, `typeToken`, `variant` |
| Struktur | `slot` — nimmt andere Blöcke auf |

Metadaten je Control: `label`, `description`, `default`, `required`, `group` (Panel-Gruppierung wie
Webflow), `min`/`max`, `visibleWhen(values)` — Framers `hidden`, positiv formuliert — und
`confidential`, das den Wert von der Auslieferung ins Markup ausnimmt (§7.5).

**Bewusst nicht dabei:** freie Farbe, freies Padding, `borderRadius`, `boxShadow`, Font-Wahl,
`cursor`. Framer hat sie alle; für Callora sind sie die Tür zum kaputten Layout. Die Gestalt-Typen
wählen ausschließlich aus `--cal-*`-Rollen und -Stufen. Wer mehr braucht, schreibt ein Theme.

`slot` als *Control-Typ* statt als Sonderkonstrukt ist Webflows Idee und sauberer als Shopwares
separates `slots:`-Feld — Verschachtelung fällt dann von selbst heraus.

**Die Typliste ist offen.** Ein Plugin kann eigene Control-Typen beitragen (§7.4) — einen
Rufnummern-Picker, eine Agenten-Auswahl. Ein beigetragener Typ liefert zwei Dinge: die
Editor-Komponente, die den Wert erfasst, und einen Serialisierungs-Vertrag, damit der Renderer
den gespeicherten Wert versteht, ohne das Plugin zu kennen. Nicht offen sind die **Gestalt**-Typen:
sie wählen ausschließlich aus `--cal-*`, und ein Plugin, das dort einen freien Farbwähler
beitragen könnte, würde die Guardrails aus E3 aushebeln.

### 4.3 Der Block-Vertrag

```ts
registerBlock({
  id: 'communication.call-list',      // == ViewId == data-callora-island
  label: 'Anrufliste',
  category: 'telephony',              // freier String
  surfaces: ['surface', 'admin'],     // flächenneutral
  requires: ['communication.active-call/v1'],
  provides: [],
  controls: { … },
  component: CallListBlock,
  preview: CallListPreview,           // optional
})

registerBlockCategory({ id: 'telephony', label: 'Telefonie', icon: 'phone' })
```

Die Kategorie ist ein **freier String mit eigenem Registrierungspunkt** — Shopwares geschlossenes
XSD-Enum ist der Fehler, den wir nicht wiederholen. Und es gibt **keine zwei Klassen von
Erweiterern**: derselbe Vertrag gilt für Host- und Plugin-Blöcke, deklarativ wie programmatisch.

### 4.4 Zwei Dinge, die den Vertrag zusammenhalten

**Server↔Client-Konsistenz erzwingen.** Die Server-Deklaration
(`HostSurfaceViewRegistration`) bleibt die Autorität für Sichtbarkeit und Claims. Ein Test prüft im
Build, dass jede Client-Block-ID eine Server-Registrierung hat und umgekehrt — in der Linie der
bestehenden Architektur-Tests (Regel erzwingen statt erklären).

**Datenbedarf zweiphasig.** Shopwares `collect`/`enrich` wird übernommen: jeder Block meldet seinen
Datenbedarf an, der Renderer sammelt über alle Blöcke, holt gebündelt und verteilt zurück. Ohne das
kostet eine Seite mit zwanzig Blöcken zwanzig Requests.

---

## 5. Kontext — lokal und geteilt

### 5.1 Zwei Kanäle, nicht einer

Der heutige `SurfaceContextChannel` ist an eine Surface-Instanz im Browser-Tab gebunden —
synchron, billig, ohne Autorität. Das bleibt. Kontext **zwischen** Surfaces muss den Tab verlassen
und serverseitig existieren; damit ist es kein UI-Zustand mehr, sondern geteilter Sitzungszustand
mit Zugriffsregel.

| | Local Context | Shared Context |
|---|---|---|
| Reichweite | Islands einer Surface-Instanz | Surfaces, Sessions, Teilnehmer |
| Ort | Browser-Tab | Server |
| Publikation | jeder Island | **nur serverseitig** |
| Zugriff | wer im Tab ist | wer den Anker trägt |
| Kosten | keine | Verbindung + Durchsetzung |

### 5.2 Der Anker

Geteilter Kontext hängt nicht am Workspace — sonst sähe jeder Portal-Besucher, was ein Agent tut.
Er hängt an einem **Anker**:

- **`subject`** — derselbe Handelnde über mehrere Surfaces. Agent Desktop und
  Videokonferenz-Surface teilen den Anker, weil sie dieselbe `SurfaceSubject`-Identität
  (`issuer` + `subjectId`) tragen. Existiert bereits aus ADR-017.
- **`conversation`** — derselbe Vorgang über verschiedene Beteiligte. Agent und Kunde hängen am
  selben Anruf, obwohl sie verschiedene Personen auf verschiedenen Surfaces sind. Ein Plugin
  erzeugt den Anker und ordnet Teilnehmer zu.

`SurfaceHandoffService` ist dabei mehr als Identitätsübergabe: er ist der natürliche Weg, einen
Anker von Surface A nach Surface B zu tragen — das Ticket führt ihn mit.

**Zugriffsregel:** Ein Wert wird mit seinem Anker veröffentlicht, und nur wer denselben Anker
nachweisen kann, liest ihn. Eine anonyme Landingpage trägt keinen Anker und sieht nichts, ohne dass
jemand daran denken muss. Zwei Surfaces mit verschiedenen Access-Modes (`Public` neben
`Authenticated`) teilen nur, was ein Anker verbindet, den beide Seiten legitim tragen — die
Access-Mode-Grenze bleibt unangetastet.

**Die Key-Deklaration.** Ein geteilter Key wird serverseitig deklariert, nicht im Browser — der
clientseitige `SurfaceContextDescriptor` (key, publisherPluginId, cardinality, validate) bleibt für
den lokalen Kanal, reicht für den geteilten aber nicht. Die serverseitige Deklaration trägt
zusätzlich:

- **Ankertyp**, an den der Key gebunden ist (`subject` oder `conversation`)
- **Zweck** als Klartext — Zweckbindung nach DSGVO, dieselbe Doku-Pflicht wie CAL0003 auf der
  C#-Seite
- **Feld-Schema mit Sichtbarkeitsstufe je Feld** — die Grundlage für die Projektion (§5.5 P1)
- **Lebensdauer** (§5.4)

Damit ist ein Key ein dokumentierter Vertrag statt eines Strings, und die Projektion je Abonnent
ist aus der Deklaration ableitbar statt in jedem Publisher neu erfunden.

### 5.3 Warum der Block das nicht merkt

**Ein Block deklariert seinen Kontextbedarf, er beschafft ihn nicht.** Der Auflöser entscheidet:
Gibt es im selben Tab einen Publisher? Dann lokal, ohne Server. Sonst — trägt der Caller einen
Anker, unter dem dieser Key veröffentlicht ist? Dann geteilt, über die Realtime-Bridge. Der
Block-Code ist in beiden Fällen identisch.

Damit ist die Topologie eine Konfigurationsentscheidung des Kunden und berührt keinen Block-Code:

- Wer nur Videokonferenz will, nimmt **ein** Surface. Alles läuft lokal, der geteilte Kontext
  kostet nichts und wird nie angefasst.
- Wer CRM, Dialer und VC trennen will, bekommt **mehrere** Surfaces, die über Anker
  zusammenhängen — **mit denselben Blöcken**.

`HostSurfaceViewRegistration.ProvidesContexts` / `RequiresContexts` — bisher totes Metadatum — wird
die Deklaration, aus der das fällt.

### 5.4 Lebensdauer

Ein geteilter Kontext braucht Ablauf; ein „aktiver Anruf" darf nicht ewig hängen, wenn ein Tab
abstürzt. Muster steht bereits: `SurfaceSessionPurgeJobHandler` mit `IRecurringJobProvider`.

### 5.5 Sicherheit und Datenschutz

Der geteilte Kontext transportiert personenbezogene Daten über Surface-Grenzen hinweg. Alles, was
im Browser ankommt, ist über DevTools, Konsole und jedes im Tab laufende Skript einsehbar — die
Kanal-API ist **keine** Zugriffsgrenze. Die Verteidigung liegt deshalb vollständig auf der
Serverseite. Sieben Prinzipien, die den Entwurf binden:

**P1 — Der Server projiziert, der Client filtert nie.**
Ein Kontext-Key deklariert ein **Schema mit Feldern**, und jedes Feld trägt eine Sichtbarkeitsstufe.
Was ausgeliefert wird, ist bereits das Minimum für den konkreten Abonnenten. Ein Agent Desktop
bekommt zu `communication.active-call/v1` die Kundenakte, ein Kundenportal am selben Anruf nur
Gesprächsstatus und Dauer. Overfetching mit clientseitiger Anzeigefilterung ist ausgeschlossen —
das ist der klassische DSGVO-Fehler und hier per Konstruktion nicht möglich.

**P2 — Anker kommen aus der Session, nie aus dem Request.**
Kein Client kann einen Anker behaupten. Der Anker wird serverseitig aus der validierten
Surface-Session bzw. `SurfaceSubject`-Identität abgeleitet. Ein Query-Parameter oder Header, der
einen Anker benennt, existiert nicht.

**P3 — Bedarfsgesteuerte Auslieferung.**
Der Server sendet einen Key nur, wenn auf der konkreten Surface mindestens ein sichtbarer Block ihn
deklariert hat. Die Datenbasis dafür existiert bereits: `RequiresContexts` auf
`HostSurfaceViewRegistration`, gefiltert durch `SurfaceSlotResolver` (Verfügbarkeit + Claims). Ein
Key, den auf dieser Surface niemand braucht, verlässt den Server nicht — unabhängig davon, ob ein
Anker ihn theoretisch erreichbar machen würde.

**P4 — Im Browser gibt es keine Plugin-Isolation, und das wird nicht behauptet.**
Alle Bundles einer Surface laufen im selben JS-Realm. Plugin A *kann* technisch
`channel.read('crm.customer-record/v1')` aufrufen, auch ohne Deklaration. Das ist nicht hart
verhinderbar, und ein Versprechen dieser Art wäre unehrlich. Es gilt dasselbe Trust-Modell wie
in-process auf dem Server (curated, signiert, ADR-013): geladen wird, was vertraut ist. **Deshalb**
sind P1 und P3 die eigentliche Verteidigung — wenn ein Wert gar nicht erst im Browser liegt, ist
gleichgültig, wer ihn dort lesen könnte.

**P5 — Nichts wird im Browser persistiert.**
Kontextwerte leben ausschließlich im Speicher. Kein `localStorage`, kein `sessionStorage`, keine
Cache-Header auf den Kontext-Endpunkten, kein Wiederherstellen nach Reload — nach dem Schließen des
Tabs ist nichts mehr da.

**P6 — Werte werden nie protokolliert.**
`diagnostics()` gibt heute schon Existenz, Publisher und Zähler zurück, nie Inhalte — das bleibt so
und gilt auch für die Server-Seite. Die vorhandenen Konsolen-Warnungen des Kanals nennen
Key-Namen, keine Werte; diese Trennung ist bindend.

**P7 — Nicht-Existenz ist ununterscheidbar von Nicht-Berechtigung.**
Ein Abonnement auf einen Key, den der Caller nicht sehen darf, liefert dasselbe wie ein Abonnement
auf einen Key, den es nicht gibt: nichts. Kein „forbidden", keine Enumeration der vorhandenen
Kontexte. Dasselbe Muster verwendet `/workspace/public/ui-chain` bereits, wo ein
`Authenticated`-Workspace für Anonyme `404` statt `403` liefert, damit die Plugin-Liste nicht
aufzählbar ist.

**Verbindungssicherheit.** Der Kontext-Socket läuft ausschließlich über `wss://`; die
Surface-Session-Cookies bleiben `Secure`/`HttpOnly`/`SameSite`. Eine WS-Verbindung lebt lange, die
Berechtigung dahinter nicht: läuft die Surface-Session ab, meldet sich der Nutzer ab, oder wird der
Identity-Provider der Surface neu zugewiesen — ADR-017 §6.3 macht `IdentityAssignedAtUtc` zur
Invalidierungsgrenze für ältere Sessions —, muss die Verbindung fallen und neu autorisiert werden.
Bei `Mixed`-Surfaces wird pro **Verbindung** bewertet, nicht pro Surface.

**Was DSGVO konkret verlangt und wo es sitzt:**

| Anforderung | Umsetzung |
|---|---|
| Zweckbindung | Jeder Kontext-Key deklariert seinen Zweck im Vertrag — dieselbe Doku-Pflicht, die CAL0003 auf der C#-Seite erzwingt |
| Datenminimierung | P1: Feld-Sichtbarkeit im Key-Schema, Projektion auf dem Server |
| Speicherbegrenzung | TTL je Kontextwert (§5.4) plus P5 im Browser |
| Löschung / Betroffenenrechte | Geteilter Kontext ist personenbezogen und muss beim Löschen eines Subjects mitgehen — `IWorkspaceDataPurgeContributor` deckt die Workspace-Ebene, die Subject-Ebene ist zu ergänzen |
| Keine unbeabsichtigte Offenlegung | P3 + P7; ein `Public`-Surface erhält keinen personenbezogenen geteilten Kontext, weil dort kein tragender Anker entsteht |

**Für den Editor:** die simulierten Kontextwerte (§7.6) sind **synthetische Beispieldaten**, niemals
Produktionswerte. Der Editor liest keinen Live-Kontext, um eine Vorschau zu füllen.

---

## 6. SDK-Familie, Komponenten, Anpassbarkeit

### 6.1 Schnitt

> **Revidiert am 2026-08-06.** Dieser Abschnitt sah ursprünglich einen flächenneutralen Kern
> `@callora/ui-core` mit zwei SDKs darauf vor. Der Kern war mit dem Composer-Canvas begründet:
> ein Block sollte im Editor und live durch denselben Code rendern. Da der Composer **nur
> Surfaces** bearbeitet, laufen seine Blöcke ohnehin immer in der Surface-Runtime — die
> Portabilität zwischen den Flächen wird nie gebraucht. Ausführliche Begründung samt
> Peer-Vergleich (Umbraco, ABP, Shopware) in
> [Frontend-Paketstruktur](../analysis/2026-08-06-frontend-paketstruktur.md).

**Zwei Pakete, je im Modul, mit Unterpfad-Exporten** — Umbracos Muster:

```
src/Administration/Resources/app/administration/   → @callora/admin
  ./extensions   registrieren, eingreifen, Dienste ersetzen
  ./components   die Cal*-Primitive
  ./tokens       Design-Tokens
  ./patterns     CalListPage & Co.

src/Surface.Rendering/Resources/app/surface/       → @callora/surface
  ./views        registerSurfaceView, Islands
  ./context      Kontext-Kanal
  ./components   Surface-Primitive
```

Beide Apache-2.0. Kein geteilter Kern, kein `sdk/`-Verzeichnis, kein eigenes Repo — letzteres
erst bei Vertrags-Freeze auf 1.0 plus erstem externen Konsumenten.

**Was dadurch entfällt:** der Link-Port (ein Admin-Paket darf `vue-router` importieren), ein
gemeinsames Vue-Global (jedes Paket behält seins), der HTTP-Port mit austauschbarem Transport,
und der Drift-Test zwischen SDK und Runtime — die Duplikation verschwindet, weil beide dasselbe
Paket werden.

**Begrifflich** übernehmen wir Shopwares Trennung: ein *SDK* ist die Fähigkeits-API — was ein
Plugin tun kann —, nicht die Bausteine. Komponenten und Tokens sind Bibliothek, kein SDK; sie
reisen im selben Paket, aber unter eigenen Unterpfaden.

### 6.2 Was die Admin-SDK löst

Punkt für Punkt gegen §1.2: typisierte `registerExtension`/`registerHook`/`registerService` statt
abgetipptem Interface, `calloraAdminPlugin()`-Preset statt duplizierter Vite-Zeilen, Zugang zu den
`Cal*`-Komponenten, `useWorkspaceContext`/`useToast`/`useConfirm`/`apiFetch`, eigene Routen pro
Plugin statt nur `/extensions/:pluginId`, Realtime aus dem Kern statt Eigenbau.

### 6.3 Extension-Point-Katalog

Lücke B/C der Landkarte, angewandt auf die UI — und hier billiger als in C#. Ein Build-Schritt
scannt `<ExtensionSlot name="…">` und `runHook('…')` und erzeugt zweierlei:

```ts
// generiert — ein Typo ist ein Compile-Fehler, kein stiller No-Op
export type AdminSlot = 'users.list.toolbar' | 'users.detail.fields' | …
export type AdminHook = 'users.before-save' | 'users.after-save' | …
```

und eine Katalog-JSON für einen `plugin:points`-Befehl und die Admin-UI. TypeScript-Literal-Unions
leisten direkt, wofür es in C# einen Analyzer braucht. Dazu ein Test, der prüft, dass jeder Punkt
im Katalog dokumentiert ist — dasselbe Prinzip wie CAL0003.

### 6.4 Drei Komponenten-Ebenen

**Primitive** (`Cal*`) — existieren, ziehen unverändert in den Kern.

**Muster** — fehlen. `CalListPage`, `CalDetailPage`, `CalSettingsSection` fassen die
wiederkehrende Anordnung zusammen **und bringen die Extension-Slots mit**. Das ist der eigentliche
Gewinn: Extension-Punkte entstehen durch Verwendung des Musters, nicht durch Disziplin — heute muss
jemand daran denken, in einer neuen Liste `<ExtensionSlot>` zu platzieren.

**Blöcke** — die editorfähige Einheit, aus Mustern und Primitiven gebaut, plus
Control-Deklaration.

### 6.5 Drei Stufen der Anpassung

**Tokens** — kein Code, existiert.

**Varianten** — vom Bauteil deklariert (`variant`, `tone`, `size`, …). Existiert bereits als
TypeScript-Union; im Kern wird es maschinenlesbarer Vertrag, sodass ein Block seine Ausprägungen
unverändert als `variant`-Control an den Editor durchreichen kann.

**Ersetzung** — **nur an markierten Punkten**. Blanko-Override ist genau das, was der
Registry-Kommentar ablehnt („no internal-structure coupling"):

```ts
export const CalDataTable = defineReplaceable('cal.data-table', CalDataTableImpl)
const Table = useComponent(CalDataTable)      // auflösend, nicht direkt importiert
```

Der Prop-Vertrag ist die Grenze, TypeScript prüft ihn. Konflikte werden mit Priority aufgelöst und
diagnostiziert, exakt wie `useService`/`getServiceConflicts`. Wo ein benannter Slot reicht
(`CalDataTable` mit `cell:<column>`-Slots), bleibt Ersetzung der Ausnahmefall.

Das spiegelt die C#-Seite: `[CalloraExtensible]` markiert, was erweiterbar ist; `IServiceDecorator<T>`
wirkt nur auf durchgeschleuste Services. Dieselbe Haltung, andere Sprache.

### 6.6 Konkretes Hindernis

`CalButton` importiert `RouterLink` aus `vue-router`. Im flächenneutralen Kern nicht tragbar. Der
Kern definiert einen `link`-Port; die Admin-Runtime registriert `RouterLink`, die Surface-Runtime
ihr eigenes Navigationselement. Beim Umzug sind weitere solcher Fäden zu erwarten.

### 6.7 Warum das den Editor trägt

Ein Block aus Primitiven bekommt Theme-Anpassung geschenkt — die Tokens kaskadieren in den Canvas —
und kann seine Varianten als Editor-Control anbieten. Ein Block mit selbstgemaltem Hex-CSS kann
beides nicht: er sieht im Canvas falsch aus und bietet dem Redakteur nichts an. Kein Zwang, aber
ein spürbarer Vorteil — und genau das hält ein Ökosystem zusammen.

---

## 7. Der Editor (Surface Composer)

### 7.1 Datenmodell

```
SurfaceLayout           key, workspaceId, surfaceKey?, name          ← Identität
 └ SurfaceLayoutVersion layoutId, versionNumber, state, document (JSON),
                        label, createdBy/At, publishedBy/At

document:
  sections[]            layout, spacing, surfaceRole, visibility, position
    └ blocks[]          blockId, region, position, config: Record<string, Binding>, visibility
```

**Sektionslayouts kommen aus dem Theme, nicht aus dem Core.** Ein Theme deklariert in `theme.json`,
welche Layouts es kann (`single`, `two-2-1`, `sidebar-left`, …) und welche Regionen darin
existieren. Der Editor bietet ausschließlich das an. Damit bleibt die Token-Achse aus ADR-014 §10
die Design-Autorität, und es stehen keine Layout-Namen im Core.

`spacing` und `surfaceRole` sind Token-Stufen, keine Werte (E3).

### 7.2 Versionierung

Der Inhalt ist ein **unveränderliches JSON-Dokument pro Version**, nicht normalisierte Tabellen je
Sektion und Block. Eine Version ist ein Snapshot und wird nie teilweise geändert: Rückrollen ist
Zeilen-Kopieren, Diff und Historie sind trivial, der Renderer liest ein Dokument statt drei Joins.
Shopware normalisiert *und* versioniert auf jeder Ebene (`cmsPageVersionId`, `cmsSectionVersionId`,
`cmsBlockVersionId`) — diese Maschinerie sparen wir uns.

Was dabei verloren geht, ist die Abfrage „welche Layouts benutzen Block X". Die wird gebraucht
(Warnung beim Deinstallieren eines Plugins), also: JSON ist die Wahrheit, dazu ein schmaler
abgeleiteter Nutzungsindex.

**Zustände:** `draft` (genau einer je Layout, Autosave schreibt hinein), `published` (genau einer,
der einzige den das Frontend sieht), `archived` (frühere Veröffentlichungen).

**Übergänge:** Bearbeiten schreibt in den Entwurf. Veröffentlichen macht den Entwurf zur neuen
`published`-Version mit fortlaufender Nummer und archiviert die bisherige. Verwerfen erzeugt den
Entwurf aus der veröffentlichten Version neu. Rückrollen kopiert eine archivierte Version **als
Entwurf** — nicht direkt live.

Autosave erzeugt **keine** Version. Nur Veröffentlichen tut das.

**Nebenläufigkeit:** optimistisches Sperren über einen Änderungsstempel auf dem Entwurf; der zweite
Speichervorgang mit veraltetem Stand bekommt einen Konflikthinweis statt eines stillen
Überschreibens. Echte Live-Kollaboration wäre ein eigenes Vorhaben.

**Aufbewahrung:** archivierte Versionen begrenzen (letzte 20 oder 90 Tage) über
`IWorkspaceDataPurgeContributor`.

### 7.3 Die Entwurfs-Garantie

Der Contract hat zwei getrennte Methoden, nicht eine mit Schalter:

```csharp
Task<SurfaceLayoutDocument?> GetPublishedAsync(workspaceKey, surfaceKey, ct);
Task<SurfaceLayoutDocument?> GetDraftAsync(layoutKey, ct);   // Operator-Berechtigung
```

Der öffentliche Renderpfad `/surface/render` ruft ausschließlich `GetPublishedAsync`. Es gibt
**kein** `?preview=true`, keinen Header, keinen Query-Parameter, mit dem sich ein Entwurf von außen
anfordern ließe — bei `Public`-Surfaces säße eine solche Lücke hinter keiner Authentifizierung.

Der Editor baut den Canvas clientseitig aus dem Draft-Dokument, das er über die Admin-API geholt
hat; er ruft `/surface/render` gar nicht auf. Das folgt aus E6 und stützt die Garantie.

### 7.4 Der Composer ist ein eigenständiges, selbst erweiterbares Plugin

Der Editor ist laut ADR-014 §10.4 ein eigenes Plugin — und zwar eines mit vollem Bürgerrecht: mit
eigenem Schema, eigenen Verträgen und einer eigenen Erweiterungsfläche. Er ist kein Sonderfall im
Core, sondern der anspruchsvollste Konsument der Plattform.

**Vertrag im Core, Daten im Plugin.** Der Core definiert genau einen Contract —
`ISurfaceLayoutSource` —, das Composer-Plugin implementiert und exportiert ihn, der
Kompositions-Renderer fragt ihn. Kein Composer installiert → kein Layout → die Surface rendert wie
heute aus `.njk`. Der Core bleibt frei von Editor-Domäne; das Plugin bekommt sein eigenes Schema
über `IPluginDbContextFactory<T>`.

**Eigene Abstractions.** Alles, was über das Lesen eines Layouts hinausgeht, gehört nicht in den
Core, sondern in ein eigenes Vertragspaket `Callora.Composer.Abstractions` — dasselbe Muster, mit
dem VideoConference gegen `Communication.Abstractions` baut (ADR-016). Ein Drittplugin referenziert
die Abstractions, nie das Composer-Plugin selbst, und erbt dessen SDK-Abhängigkeiten nicht.

**Was ein Plugin am Composer erweitern kann:**

| Erweiterungspunkt | Wofür |
|---|---|
| **Eigene Control-Typen** | Ein Rufnummern-Picker, eine Agenten-Auswahl, ein Kontakt-Selektor — Typen, die das Basisschema (§4.2) nicht kennt |
| **Inspektor-Beiträge** | Ein zusätzlicher Reiter oder Abschnitt im Konfig-Panel eines Blocks |
| **Werkzeug-Beiträge** | Eigene Aktionen in Canvas-Toolbar oder Kontextmenü |
| **Validierungs-Beiträge** | Zusätzliche Guardrails vor dem Veröffentlichen, mit Befund und Schweregrad |
| **Layout-Vorlagen** | Fertige Startpunkte („Agenten-Arbeitsplatz"), die ein Plugin mitbringt |
| **Block-Quellen** | Blöcke aus einer anderen Herkunft als der lokalen Registrierung |

**Hooks, mutable und cancelable** — dasselbe Modell wie in der Admin-Shell:
`composer.before-publish` (abbrechbar, etwa wenn eine Freigabe fehlt), `composer.after-publish`,
`composer.before-block-insert`, `composer.before-layout-save`. Die Namen gehören in denselben
generierten Katalog (§6.3), damit auch Composer-Erweiterungen compiler-geführt sind.

**Grenze:** Der Composer erweitert die **Bearbeitung**, nicht die Zugriffsregeln. Ein
Validierungs-Beitrag kann eine Veröffentlichung verhindern, aber kein Beitrag kann die
Entwurfs-Garantie (§7.3), die Token-Guardrails (§7.7) oder die Kontext-Durchsetzung (§5.5)
aufweichen — das sind Eigenschaften des Renderpfads und des Servers, nicht des Editors.

#### Keine Sonderrechte — der Composer ist der Härtetest der Plattform

Der Composer hält **dieselben Plugin-Vorgaben** ein, die für jeden Dritten gelten: Einstieg über
`IHostManagedPlugin.StartAsync` mit `context.Export<T>`, ein `registry.json` mit
`contractVersion`, `capabilities` und `extensions`, ALC-Isolation, eigenes Schema über
`IPluginDbContextFactory<T>`, signiertes Content-Manifest, Admin-UI als IIFE-Bundle unter
`Resources/public/admin`, CAL0001/0002/0003-konform. Die Guides unter
`docs-site/guides/fundamentals/` sind für ihn nicht Referenz, sondern Vorschrift.

Daraus folgt ein Prüfmaßstab, der über den Composer hinausreicht: **Braucht er etwas, das die
Plattform einem Drittplugin nicht gäbe, ist das ein Plattform-Befund — nicht ein
Composer-Sonderfall.** Der Composer ist damit für die Plugin-Plattform, was das
Communication-Bundle für die Admin-SDK ist: der Konsument, an dem sich zeigt, ob die Fläche
trägt.

Dass `ISurfaceLayoutSource` im Core liegt, widerspricht dem nicht — der Contract ist offen. Ein
anderer Anbieter kann einen eigenen Editor bauen, der ihn bedient; der Composer ist nur der erste
Implementierer, nicht der privilegierte.

**Ein Befund gibt es bereits.** Der Canvas muss Surface-Block-Bundles im Admin-Kontext laden, aber
`loader.ts` holt heute ausschließlich Assets mit `surface === 'admin'` aus dem Manifest. Der
Composer könnte sich das selbst bauen — Manifest holen, Skripte injizieren, wie der Loader es tut —
und das wären keine Sonderrechte, sondern gewöhnliche Plugin-Arbeit. Aber jeder Editor täte es
dann erneut, und die Fehlertoleranz und Ladetelemetrie (`__calloraSurfaceLoad`) gäbe es doppelt.
Deshalb gehört das Bundle-Laden als Fähigkeit nach `@callora/surface` — parametrisiert nach
Ziel-Surface statt fest auf `admin`, und dort, weil der Canvas Surface-Bundles lädt.

**Gebaut wird es mit Baustein 7, nicht mit Baustein 1.** Vorher gibt es keinen Konsumenten, und
eine Kern-Fähigkeit ohne Konsumenten ist eine Vermutung über die richtige Schnittstelle. Das ist
dieselbe Regel, nach der jeder Baustein hier einen ersten Konsumenten benennt.

### 7.5 Kompositions-Renderer

Der Renderer emittiert je Sektion einen Container mit Token-Attributen und je Block genau das
Format, das `mount.ts` heute schon versteht:

```html
<div data-callora-island="communication.call-list"
     data-callora-props='{"title":"Aktive Anrufe","max":5}'></div>
```

An der Surface-Runtime muss sich **nichts** ändern; der Renderer ist additiv.

**Die Block-Konfiguration wird vor dem Rendern serverseitig gefiltert.** `data-callora-props` steht
als Attribut im ausgelieferten HTML und ist damit für jeden lesbar, der die Seite abruft — bei einer
`Public`-Surface auch ohne Anmeldung. Es gilt deshalb dasselbe Prinzip, das `SurfaceSlotResolver`
bereits auf Views anwendet: gefiltert wird auf dem Server, bevor Markup existiert, nicht per CSS
oder im Client. Konkret heißt das: Controls können als vertraulich deklariert werden und erscheinen
dann nie im Attribut, `context`-Bindungen werden als Bindung serialisiert und nie als aufgelöster
Wert, und Werte aus vertraulichen Quellen fallen ganz heraus — analog zur Secret-Filterung, die
`WorkspacePublicThemeResolver` bei Theme-Settings schon vornimmt.

**Ein Block hat genau eine Darstellung — die Vue-Komponente.** Ein zweiter, serverseitiger
Renderpfad (`.njk`-Partial je Block) wurde erwogen und verworfen: zwei Implementierungen derselben
Darstellung driften auseinander, und im Direktmanipulations-Canvas bräuchte jede Konfig-Änderung
einen Server-Roundtrip. Konsequenz: editor-gebaute Seiten sind Islands ohne SSR-Inhalt. Für
SEO-Landingpages bleibt der `.njk`-Template-Weg für Entwickler; für Arbeitsplätze und Portale — das
Hauptziel — spielt es keine Rolle.

### 7.6 Der Canvas ist die Vorschau

Kein iframe, kein Postmessage-Protokoll, kein zweiter Renderpfad, keine Vorschau-Drift. Drag & Drop
passiert auf den echten DOM-Elementen. Vier Bedingungen:

1. **Ein Vue-Global statt zwei.** Heute `window.CalloraVue` (Surface) und `window.CalloraAdmin.vue`
   (Admin); ein Block-Bundle ist gegen genau einen gebaut. Der Kern definiert einen gemeinsamen
   Namen, den beide Runtimes bereitstellen. Die alten Namen werden **ersetzt, nicht aliasiert** —
   es gibt keine fremden Bundles, und ein Alias, den niemand braucht, ist nur ein zweiter Weg zum
   selben Ziel.
2. **Theme-Tokens in den Canvas scopen.** Möglich, weil `tokens.scss` bewusst Custom Properties
   statt SCSS-Variablen nutzt. Aufwand liegt bei Plugin-Themes, deren CSS auf `:root` zielt — das
   muss beim Publizieren gescoped werden.
3. **Sektions-CSS kommt vom Theme in den Canvas** — dasselbe Stylesheet, das live gilt.
4. **Eine Darstellung je Block** (§7.5).

**Klick-Konflikt:** ein Edit-Layer über den Blöcken fängt Pointer-Events auf Blockebene ab, mit
einem „Interaktiv testen"-Umschalter, der ihn kurzzeitig durchlässt.

**Simulierte Kontextwerte:** der Editor kann Kontext-Keys mit Beispielwerten belegen — der
Redakteur füllt `communication.active-call/v1` mit einem simulierten Anruf und sieht den
dynamischen Block arbeiten, ohne dass jemand anruft. Der Kanal kann das bereits; es braucht nur
einen Editor-Publisher. Funktioniert für beide Kontext-Kanäle gleich.

### 7.7 Guardrails

Aus dem Vertrag heraus, nicht als Editor-Sonderlogik: das Panel wird generiert (keine px-Felder,
keine Farbwähler), Sektionslayouts nur aus dem Theme, Blöcke nur in Regionen des gewählten Layouts,
`surfaces` filtert das Angebot.

### 7.8 Fehlerverhalten

- **Verwaister Block** (Plugin deinstalliert): Editor zeigt einen benannten Platzhalter mit
  Hinweis, das Frontend lässt ihn weg. Das Layout bleibt intakt und wird wieder vollständig, sobald
  das Plugin zurückkommt.
- **Theme kennt das Sektionslayout nicht mehr** (Theme-Wechsel): Fallback auf `single`, Warnung im
  Editor, Inhalt geht nie verloren.
- **Kontext-Key ohne Publisher**: der Block rendert seinen `default`-Zustand. Der Editor zeigt es
  an, das Frontend schweigt.
- **Kontext-Key ohne erreichbaren Anker**: der Editor warnt beim Einsetzen des Blocks, statt ihn
  später stumm leer zu lassen.
- **Renderfehler**: wie heute bei `.njk` — geloggt, Fallback auf die nächst-einfachere Stufe, nie
  ein Fehler beim Besucher.

---

## 8. Umsetzungsschnitt

| # | Baustein | Liefert | Erster Konsument |
|---|---|---|---|
| 1 | `@callora/admin`: Paket-Identität, generierter Extension-Point-Katalog, typisierte Registrierung, `defineReplaceable`, Muster-Ebene, Preset, Bibliotheks-Build | **Erstwunsch erfüllt** | Communication-Admin-Bundle umstellen |
| 2 | `@callora/surface`: `custom/surface-sdk` in die Runtime auflösen, Paket-Identität, `params` typisiert | Vertrag nur noch einmal deklariert | SurfaceDemo |
| 3 | Server↔Client-Konsistenztest, CI/Dependabot, READMEs | Drift maschinell bemerkt | — |
| 4a | Realtime-Bridge: WS an lokale Kontext-Keys, `ProvidesContexts` funktional | Dynamik im Ein-Surface-Fall | Communication-Block auf eingehende Anrufe |
| 4b | Shared Context: Anker, serverseitige Publikation, Key-Schema mit Feld-Sichtbarkeit, Projektion, Anker-Durchsetzung, Ablauf, Verbindungsinvalidierung | Mehr-Surface-Topologien | Agent Desktop + VC-Surface |
| 5 | Block-Vertrag + Control-Schema + generierter Panel-Renderer | Editor-Fundament | ein Host-Block |
| 6 | `ISurfaceLayoutSource` + Kompositions-Renderer | Layout → Islands | — |
| 7 | Composer-Plugin: `Callora.Composer.Abstractions`, Canvas, Drag & Drop, Entwurf/Veröffentlichen, eigene Erweiterungsfläche, Bundle-Loader nach Ziel-Surface in `@callora/surface` (§7.4) | Surface Composer | — |

Der Schnitt ist so gelegt, dass nach **3** angehalten werden kann und trotzdem geliefert ist, was
zuerst gebraucht wird. **4a** trägt den Ein-Surface-Fall vollständig; **4b** ist der Aufpreis für
Mehr-Surface-Topologien und kann später kommen, ohne dass ein Block umgeschrieben wird.

---

## 9. Offene Punkte und bewusste Nicht-Ziele

**Offen:**

- **Name des Editors.** Arbeitstitel *Surface Composer*. Alternativen: *Bühnen* (poetische Linie
  wie „Erlebniswelten"; die Metapher trägt hier gut, weil auf einer Bühne Dinge *auftreten*, was zum
  Live-Charakter passt), *Arbeitsflächen* (nüchtern, deckt Portal und Agentenplatz gleichermaßen).
- **Geplantes Veröffentlichen** (`publishAtUtc` + `IRecurringJobProvider`) — vom Modell getragen,
  in v1 nicht gebaut.
- **Scoping-Verfahren für Plugin-Theme-CSS** im Canvas (CSS-Rewrite beim Publizieren vs. `@scope`)
  — Detailentscheidung zu Baustein 7.
- **Subject-Löschung** (§5.5): geteilter Kontext ist personenbezogen und muss beim Löschen eines
  Subjects mitgehen. `IWorkspaceDataPurgeContributor` deckt die Workspace-Ebene; ob die
  Subject-Ebene eine Erweiterung dieses Contracts oder einen eigenen bekommt, ist offen.
- **Protokollierung von Kontextzugriffen** — ob DSGVO-Rechenschaftspflicht hier ein Zugriffsprotokoll
  verlangt (wer hat wann welchen personenbezogenen Kontext erhalten) oder ob die
  Anker-Durchsetzung ohne Protokoll genügt, ist juristisch zu klären, nicht technisch.

**Bewusste Nicht-Ziele:**

- **SSR-Inhalt für editor-gebaute Seiten** — Begründung in §7.5. Der `.njk`-Weg bleibt für
  SEO-Seiten.
- **Freies Raster mit Breakpoint-Kontrolle** — verworfen zugunsten E3.
- **Echte Live-Kollaboration im Editor** — eigenes Vorhaben; v1 hat optimistisches Sperren.
- **Routing in der Surface-SDK** — nachrangig; mit dem Editor werden Seiten zu Layouts, und
  Navigation zwischen Layouts ist Sache des Layouts.

---

## 10. Tests und Governance

Neben den üblichen Unit-Tests Tests, die Architektur- und Sicherheitsregeln festnageln — in der
Linie der bestehenden Architektur-Tests. Vertragsregeln:

1. Jede Client-Block-ID hat eine Server-Registrierung und umgekehrt.
2. Jeder Slot und Hook im generierten Katalog ist dokumentiert (Prinzip CAL0003).
3. Ein generiertes Konfig-Panel und Block-CSS enthalten keine freien Pixel- oder Hex-Werte.

Sicherheitsregeln — jede als Test, nicht als Vorsatz:

4. **Der öffentliche Renderpfad ruft niemals `GetDraftAsync`.**
5. **Kein Anker stammt aus Request-Daten** (P2): der Anker-Auflöser akzeptiert keine Query-,
   Header- oder Body-Quelle.
6. **Projektion vor Auslieferung** (P1): ein Kontextwert erreicht den Transport nur in der für den
   Abonnenten projizierten Form; ein unprojizierter Wert ist ein Testfehler.
7. **Bedarfsgesteuerte Auslieferung** (P3): ein Key ohne deklarierenden, sichtbaren Block auf der
   Surface wird nicht gesendet.
8. **Ununterscheidbarkeit** (P7): Abonnement auf einen unberechtigten und auf einen nicht
   existierenden Key liefern identische Antworten.
9. **Keine Persistenz im Browser** (P5): die Runtime schreibt keine Kontextwerte in
   `localStorage`/`sessionStorage`; Kontext-Antworten tragen `no-store`.
10. **Keine Werte in Protokollen** (P6): weder Server-Logs noch `diagnostics()` geben Inhalte aus.
11. **Vertrauliche Controls erscheinen nicht in `data-callora-props`**, und `context`-Bindungen
    werden dort nie aufgelöst serialisiert.
12. **Verbindungsinvalidierung**: nach Session-Ablauf, Abmeldung oder Neuzuweisung des
    Identity-Providers (`IdentityAssignedAtUtc`) wird die Kontext-Verbindung beendet.

---

## 11. Bezug zu bestehenden ADRs

- **ADR-014 §9/§10** — Struktur- und Token-Achse des Surface-Bundles. Der Editor sitzt auf der
  Token-Achse auf; Sektionslayouts kommen aus dem Theme, damit die Achsentrennung erhalten bleibt.
- **ADR-014 §10.3** — Admin-Shell hat feste Struktur mit Extension-Points, Surface hat variable
  Struktur. Der flächenneutrale Block-Vertrag verletzt das nicht: er ändert nicht die Admin-Struktur,
  er macht nur dieselben Bausteine dort verwendbar.
- **ADR-014 §10.4** — *„Ein späterer Page-Builder wird als eigenes Plugin ergänzt … die Bundle- und
  Block-Verträge dieser ADR sind die Grundlage."* Dieses Design ist die Ausführung dieser Zusage.
- **ADR-015** — Nunjucks-auf-Jint bleibt unverändert der SSR-Renderer; der Kompositions-Renderer
  tritt additiv daneben.
- **ADR-017** — `SurfaceSubject` und `SurfaceHandoffService` tragen die Anker des Shared Context
  (§5.2).

Ob dieses Design eine eigene ADR nach sich zieht (insbesondere für den Shared Context als neue
serverseitige Fläche), ist beim Übergang in die Umsetzung zu entscheiden.

## 12. Anschließendes Vorhaben

[Produkt-Telemetrie über Struktur-Kennzahlen](./2026-08-06-produkt-telemetrie-struktur-kennzahlen-design.md)
leitet aus dem Layout-Dokument (§7.1/§7.2) Kennzahlen darüber ab, **was** Kunden bauen — nie, wer.
Die Fehlerfälle aus §7.8 werden dort zu Fehlersignalen. Eigenes Vorhaben, nicht Teil dieses
Schnitts.
