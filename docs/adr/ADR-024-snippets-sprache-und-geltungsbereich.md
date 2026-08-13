# ADR-024 — Snippets: Sprache und Geltungsbereich sind zwei Achsen

**Status:** Proposed
**Datum:** 2026-08-13
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-019 — Surfaces als Baum (Vererbung entlang der Ahnenkette)
* ADR-020 — Repo-Schnitt: Plugins in eigenen Repositories
* Issue #273 — Snippet-System für Core und Plugins
* `SystemConfigResolver` / `SystemConfigScopes` — die vorhandene Geltungsbereichs-Kette
* Vorbild und Abgrenzung: Shopware 6 Snippet-System

---

## 1. Kontext

`Locale` ist durch den ganzen Stack verkabelt und wird nirgends verbraucht. Der Wert wandert
von `WorkspaceSurface.Locale` über `EffectiveSurface` in den Renderpfad und von dort bis in
die Plugins — kein einziger Konsument übersetzt damit Text. Der Vorgabewert steht hart auf
`"de"`. Gleichzeitig gibt es keine Snippet-Tabelle, kein `vue-i18n` und rund 60 `.vue`-Dateien
mit fest eingetragenen deutschen Texten.

Es fehlt also nicht die Leitung, sondern der Abnehmer.

Die Frage, an der der Zuschnitt hängt, ist nicht „welches Dateiformat", sondern **was die
Wahrheit ist** — daran entscheidet sich, was ein Plugin-Update mit den Änderungen des
Betreibers macht. Drei Modelle sind im Umlauf:

| Modell | Wer | Update überschreibt die Änderung des Betreibers? |
|---|---|---|
| Nur Dateien | Symfony, Django, TYPO3, .NET `.resx` | Nein — aber Ändern heißt Deployen |
| Datei = Basis, Datenbank = nur Abweichungen | Shopware, Magento 2 | Nein, und eine Admin-Oberfläche ist möglich |
| Datenbank = Wahrheit, Datei nur Startwert | Drupal, WordPress mit Loco | **Ja — oder das Update muss raten** |

Das dritte Modell ist der Fehler, den dieses Repository mit dem Demo-Admin bereits einmal
bezahlt hat (#282): Sobald die Datenbank die Wahrheit ist, kann ein Update nicht mehr
unterscheiden, ob ein Wert vom Betreiber stammt oder aus der Vorgängerversion des Plugins.
Drupal löst das mit einem `customized`-Flag je Zeile — das funktioniert, aber jeder Import
muss es beachten, und wer es einmal übersieht, löscht stillschweigend fremde Arbeit.

Das erste Modell scheidet aus, weil #273 ausdrücklich verlangt, dass ein Betreiber im Admin
überschreibt, ohne das Plugin anzufassen.

### Warum Shopwares Zuschnitt nicht übernommen wird

Shopware kann Snippets pro Verkaufskanal auflösen, aber über einen Umweg: Ein `snippet_set`
ist **an eine Sprache gebunden**, und der Verkaufskanal wählt Sets aus. Wer für zwei Kanäle
unterschiedliche *deutsche* Texte will, braucht zwei deutsche Sets — und die erben nichts
voneinander. Ab da wird doppelt gepflegt.

Der Grund ist ein Konstruktionsfehler, kein Umsetzungsdetail: **Sprache und Geltungsbereich
werden in eine Achse gepresst.** Sie sind aber unabhängig.

## 2. Entscheidung

Ein Snippet-Wert wird adressiert durch **(Schlüssel, Locale, Geltungsbereich)**. Es gibt
**zwei getrennte Fallback-Ketten**:

```
Geltungsbereich:  workspace → tenant → global → Paketdatei (Basis)
Locale:           de-DE → de → Vorgabe-Locale
```

**Der Geltungsbereich wird zuerst durchlaufen, die Locale erst innerhalb.**

```
Anfrage: key = "cart.title", locale = "de-DE", workspace = "acme"

  workspace/de-DE  ─┐
  workspace/de      │ ← Treffer: "Bestellung"
  tenant/de-DE      │
  tenant/de         │   nicht mehr befragt
  global/de-DE      │
  global/de         │
  datei/de-DE       │   "Warenkorb"
  datei/de         ─┘
```

Weiter gilt:

1. **Die Datenbank enthält ausschließlich Abweichungen.** Beim Anlegen eines Geltungsbereichs
   wird nichts kopiert. Ein Plugin-Update tauscht nur die Basisebene.
2. **Die Basis kommt als Datei im Paket**, deklariert in `registry.json` unter `snippets`,
   eingelesen beim Installieren und Aktualisieren — dieselbe Mechanik wie
   `RegistryConfigSchemaSyncService` für `config.fields`.
3. **Kein Snippet-Set.** Der Workspace *ist* der Geltungsbereich.
4. **Die Locale kommt aus dem Flächenbaum**, wie heute schon: `EffectiveSurface` löst sie über
   `SurfaceTree.Inherited` entlang der Ahnenkette auf. Sie wird nicht neu erfunden.
5. **Admin-Texte sind Geltungsbereich `global`** mit der Locale des Operators. Kein zweites
   System, aber auch kein Workspace-Filter an einer Stelle, an der es keinen Workspace gibt.
6. **Aufgelöst wird je (Kette, Locale) als ganzes Wörterbuch**, nicht je Schlüssel.
7. **Plugins konsumieren über `IStringLocalizer`**, den .NET-Standardvertrag; die Kette steckt
   in einer eigenen `IStringLocalizerFactory` dahinter.

## 3. Warum so

### Warum der Geltungsbereich vor der Locale steht

Andersherum käme im Beispiel oben „Warenkorb" heraus, weil `datei/de-DE` spezifischer wäre als
`workspace/de`. Ein Betreiber, der einmal „Bestellung" tippt, müsste das dann für `de`,
`de-DE`, `de-AT` und `de-CH` einzeln tun — genau die Doppelpflege, die Shopwares Sets
erzwingen, nur an anderer Stelle.

Der Satz, der die Reihenfolge trägt: **Ein Override ist eine Absicht, eine Regionalvariante nur
eine Verfeinerung.** Absicht schlägt Verfeinerung.

### Warum kein Snippet-Set

Mehrsprachigkeit in einem Workspace ist damit kein Sonderfall, sondern derselbe
Geltungsbereich mit anderer Locale. Zwei Workspaces mit je zwei Sprachen sind vier
Auflösungen aus zwei Einträgen — nicht vier Sets, die man synchron halten muss.

Die Kette gibt es außerdem schon: `SystemConfigResolver.BuildScopeChain(tenantKey,
workspaceKey)` ist `public static` und liefert genau `global → tenant → workspace`. Betreiber
kennen sie aus der Konfiguration. Ein zweites Konzept daneben wäre neu zu erklären und
verhielte sich anders.

### Warum nur Abweichungen in der Datenbank

„Was hat der Betreiber geändert?" ist dann eine Abfrage und kein Diff-Lauf gegen die
Paketdateien. Ein Override zu löschen heißt zurück zur Basis, ohne dass jemand die Basis
kennen muss. Und es gibt **keinen Fall, in dem ein Update raten müsste** — die Basis gehört
dem Paket, die Abweichung dem Betreiber, und beide liegen an getrennten Orten.

### Warum die Locale nicht pro Fläche überschreibbar wird

#273 stellt die Frage, ob Snippets pro Fläche gelten sollen. Nein: Die Fläche steuert bereits
die **Sprache** über die Vererbung im Baum, und das ist der Freiheitsgrad, den man dort
braucht. Eine vierte Auflösungsebene multiplizierte den Cache-Schlüsselraum mit der Zahl der
Flächen — für einen Anwendungsfall, bei dem „dann nimm eine eigene Fläche" die bessere Antwort
ist.

### Warum `IStringLocalizer`

Plugin-Autoren kennen den Vertrag aus jedem ASP.NET-Projekt. Der Unterschied zu einem eigenen
Callora-Port ist im Code gering und beim Einarbeiten groß. Die Auflösungskette bleibt
vollständig hinter der Factory — Plugins sehen nur den Standard.

## 4. Speicherung und Auflösung

### Basis (Paketdatei)

```jsonc
// registry.json
{
  "snippets": {
    "de-DE": "snippets/de-DE.json",
    "en-GB": "snippets/en-GB.json"
  }
}
```

Flache Schlüssel mit Punktnotation, Präfix = `pluginId`, damit zwei Plugins sich nicht
überschreiben können:

```json
{ "composer.editor.save": "Speichern", "composer.editor.discard": "Verwerfen" }
```

Eingelesen beim Installieren und Aktualisieren; beim Deinstallieren entfernt — analog zu
`ClearPluginDefinitionsAsync`. Overrides des Betreibers bleiben dabei **stehen**, weil sie in
einer anderen Tabelle liegen; ein Wiedereinspielen des Plugins stellt sie damit ohne Zutun
wieder her.

### Abweichung (Datenbank)

```
snippet_override
  id            uuid
  snippet_key   text     -- "composer.editor.save"
  locale        text     -- "de-DE"
  scope         text     -- global | tenant | workspace
  scope_key     text     -- "" | tenantKey | workspaceKey
  value         text
  updated_at    timestamptz
  updated_by    text

  UNIQUE (snippet_key, locale, scope, scope_key)
```

`scope_key` wird **ordinal** verglichen, nicht case-insensitiv — dieselbe Begründung wie in
`SystemConfigResolver`: Workspace-Schlüssel werden nirgends kleingeschrieben, und ein
Vergleich, der die Schreibweise ignoriert, macht aus zwei getrennten Workspaces einen.

### Auflösung und Cache

Gecacht wird ein fertig aufgelöstes Wörterbuch je **(Geltungsbereichs-Kette, Locale)**, nicht
je Schlüssel. Das ist die Granularität, in der auch invalidiert wird: Ein geschriebener
Override trifft genau ein Paar. Der Renderpfad zieht damit **einen** Eintrag statt N
Abfragen — sonst wäre das der nächste Hot-Path-Befund, wie ihn #268 für die Flächenroute und
#280 für das Theme hatte.

Muster: `CachedWorkspaceTemplateResolutionService` mit `InvalidateWorkspace` /
`InvalidateTenant` / `InvalidateAll`. Ein Schreibvorgang im globalen Bereich invalidiert alles,
einer im Tenant dessen Workspaces, einer im Workspace nur diesen.

## 5. Konsequenzen

**Der Renderpfad bekommt einen Nunjucks-Global.** `callora_t('key')` reiht sich in die
vorhandenen `callora_slot`, `callora_view`, `callora_navigation` ein und liest — wie die
anderen — nur, was der Host bereits in den Kontext aufgelöst hat. Kein Datenbankzugriff im
Template.

**Die Vue-Inseln bekommen ihre Texte aus dem SSR-Payload**, nicht per eigener Abfrage. Sonst
wäre jede Insel eine zusätzliche Anfrage pro Fläche, und Server- und Clienttexte könnten
auseinanderlaufen. Der Renderer muss dafür wissen, welche Schlüssel eine Insel braucht — der
Block deklariert sie, so wie er heute seine Daten deklariert.

**Die Admin-SPA bekommt `vue-i18n`** mit einem Nachrichten-Loader gegen den Snippet-Endpunkt.

**Migration der 60 `.vue`-Dateien schrittweise, mit Fallback auf den festen Text.** Der
Übersetzungsaufruf gibt den mitgegebenen Vorgabewert zurück, wenn kein Snippet existiert:

```vue
{{ t('admin.user.create', 'Benutzer anlegen') }}
```

Damit ist jede Datei einzeln umstellbar, und keine Zwischenstufe zeigt Schlüssel statt Text.
Der Umbau wird eine Reihe kleiner PRs statt eines großen. Ein Gate zählt die verbliebenen
festen Texte und lässt die Zahl nur sinken — dieselbe Mechanik wie die Baselines in
`ArchitectureRulesTests`.

**Die Vorgabe `"de"` im Renderpfad bleibt vorerst**, wird aber zu einer Option statt einer
Konstante.

## 6. Abgrenzung

**Keine Übersetzung von Inhaltsdaten.** Snippets sind Oberflächentexte. Ein mehrsprachiger
Flächeninhalt ist eine andere Frage mit anderer Antwort (Feld-Übersetzung am Datensatz) und
gehört nicht in diese ADR.

**Keine Übersetzungs-Werkzeugkette.** Kein Import/Export von `.po`, kein Anschluss an
Übersetzungsdienste. Beides ist später additiv möglich, weil die Basis eine Datei ist.

**Keine automatische Spracherkennung** aus `Accept-Language`. Die Locale kommt aus dem
Flächenbaum. Ein Sprachumschalter für Besucher setzt eine Liste der angebotenen Locales am
Workspace voraus, die es heute nicht gibt — eigener Schritt, eigene Entscheidung.

## 7. Offen

**Wie deklariert ein Block seine Snippet-Schlüssel?** Der SSR-Payload soll nur mitgeben, was
eine Insel braucht — alles mitzugeben wäre einfacher, aber das Wörterbuch wächst mit jedem
Plugin, und es liefe über die Leitung zu jedem Besucher. Zu klären zusammen mit ADR-023, das
für Blöcke bereits eine Deklaration offen hat.

**Was passiert mit einem Override, dessen Schlüssel aus dem Plugin verschwindet?** Er wird
unsichtbar, aber bleibt stehen. Stehen zu lassen ist richtig — ein Downgrade oder ein
zurückgenommenes Refactoring stellt ihn wieder her. Ob die Admin-Oberfläche solche
verwaisten Einträge zeigt, ist eine Anzeigefrage und keine Vertragsfrage.

**Vorgabe-Locale eines Workspaces.** Die Locale-Kette endet heute bei `"de"`. Ein Feld am
Workspace wäre der naheliegende Endpunkt, ist aber erst nötig, wenn eine zweite Sprache
tatsächlich ausgeliefert wird.
