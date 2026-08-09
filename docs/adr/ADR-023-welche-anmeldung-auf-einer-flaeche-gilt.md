# ADR-023 — Welche Anmeldung auf einer Fläche gilt

**Status:** Accepted
**Datum:** 2026-08-09
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-014 — Surface-Engine (§6.1 Access Modes)
* ADR-017 — Surface-Identität und Session-Transport (§6.1 Zugriffsentscheidung)
* ADR-019 — Surfaces als Baum (§4 Sichtbarkeit je Knoten)

> **Supersedes:** Dieses ADR löst **ADR-014 §6.1** (Public / Authenticated / Mixed) und die
> Zugriffsentscheidung aus **ADR-017 §6.1** ab. An ihre Stelle tritt eine Achse mit derselben
> Zahl von Werten, die aber eine andere Frage beantwortet.

---

## 1. Kontext

### 1.1 Was galt

Eine Fläche trug einen Zugriffsmodus: `Public`, `Authenticated` oder `Mixed`. Er beantwortete
„muss man angemeldet sein?".

### 1.2 Zwei Befunde

**`Public` und `Mixed` sind dasselbe.** Im Gate steht:

```csharp
// Public and Mixed serve anonymously; Mixed's per-route protection belongs to
// whoever owns those routes, not to the shell.
if (surface.AccessMode != SurfaceAccessMode.Authenticated) return null;
```

`Mixed` war eine Absichtserklärung — „irgendwo darunter liegt Geschütztes" — aus der Zeit, als
eine Fläche eine Einheit war. Seit ADR-019 gilt der Modus **pro Knoten**: Ein öffentliches
Impressum unter einem geschützten Portal ist eine Baumfrage, keine Modusfrage. Der dritte Wert
trägt seither nichts mehr.

**`Authenticated` bedeutet zweierlei.** Der Host verzweigt bereits:

```csharp
return string.IsNullOrWhiteSpace(surface.IdentityPluginId)
    ? LoginRedirect(surface, httpContext)   // die Operator-Anmeldung
    : Results.Unauthorized();               // das Identity-Plugin
```

Zwei grundverschiedene Anmeldungen hinter einem Namen. Die Wahl trifft nicht der Betreiber,
sondern das Vorhandensein eines Plugins.

### 1.3 Was daraus folgte

Ein im Admin angemeldeter Operator kommt auf eine Fläche ohne Identity-Plugin **herein**, bringt
aber **keine Claims** mit — Surface-Claims stammen ausschließlich vom Identitätsanbieter der
Fläche. Jede Ansicht und jeder Block mit einer Anforderung war für ihn unerreichbar:

> This account is missing the 'communication.calls' claim with value 'read'.

Er ist der Betreiber der Anlage und darf im Admin telefonieren. Auf seiner eigenen Fläche nicht.

---

## 2. Entscheidung

Die Achse beantwortet künftig: **Welche Anmeldung gilt auf dieser Fläche?**

| Wert | Bedeutung |
| --- | --- |
| `Public` | Keine Anmeldung verlangt. Besucher sind Gäste. |
| `SurfaceIdentity` | Das der Fläche zugewiesene Identity-Plugin. Ohne Anmeldung: 401, den Anmeldeweg besitzt das Plugin. |
| `Administration` | Die Operator-Anmeldung des Hosts. Ohne Anmeldung: Umleitung zum Admin-Login. |

**Bei `Administration` werden die RBAC-Berechtigungen des Operators zu Surface-Claims.** Die
Zerlegung ist verlustfrei und existiert bereits: `communication.calls.read` wird zu Claim
`communication.calls` mit Wert `read` — genau das Format, das eine Ansicht oder ein Block prüft.

**Ein Operator bringt genau die Berechtigungen mit, die sein Principal trägt** — nicht mehr.
Die Wildcard `*` (Maschinenschlüssel) wird **nicht** aufgelöst: Sie hat keinen Funktionsteil, aus
dem sich etwas ableiten ließe, und „jeder Claim" hier zu erfinden hieße, dass die Flächenseite
Wildcard-Semantik lernen müsste — die zweite Stelle, die dieselbe Frage beantwortet. Ein
SuperAdmin hält damit die Rechte seiner Rollen; wer per API-Schlüssel kommt, hält keine.

**Zwei Claim-Namen sind reserviert.** Die Workspace-Bindung (`host.workspace-key`) wird nie aus
einer Berechtigung abgeleitet, auch dann nicht, wenn sie fehlt. Ein Schutz nach dem Muster „nur
wenn noch nicht gesetzt" hätte genügt, um ein Überschreiben zu verhindern — aber nicht, um ein
**Erfinden** zu verhindern: Ohne gesetzte Bindung hätte eine Berechtigung namens
`host.workspace-key.read` eine Zugehörigkeit behauptet, die niemand vergeben hat. Die
Mandantengrenze hinge dann an einem Rollennamen.

### 2.1 ADR-017 §7 gilt weiter — enger gefasst

ADR-017 §7 verbot ausdrücklich, Admin-Berechtigungen als Surface-Claims zu führen. Das war
richtig, **solange die Host-Identitätsquelle für jede Fläche galt, der ein Identity-Plugin
fehlte**: Eine öffentliche Website hätte die Rechte dessen bekommen, der zufällig woanders
angemeldet war.

`Administration` macht daraus eine erklärte Wahl an genau einem Knoten. Das Verbot bleibt für
`Public` und `SurfaceIdentity` unverändert bestehen und ist durch eine Gegenprobe abgesichert.

---

## 3. Warum so

**Warum keine zweite Achse „Quelle" neben dem bisherigen Modus?**
Weil die beiden Fragen nicht unabhängig sind: „Anmeldung verlangt" ohne „von wem" ist unbeantwortbar,
und der Host beantwortet es heute schon — nur verdeckt. Zwei Achsen hätten vier Kombinationen
ergeben, von denen zwei unsinnig sind (`Public` + „Pflicht"), und die dritte (`Public` + „Operator
zählt mit") ist ein Sichtbarkeits-, kein Zugangsfall: Wer einem Gast mehr zeigen will, gewährt
Claims an der Fläche (`GrantedClaims`).

**Warum verschwindet `Mixed` ersatzlos?**
Es tat nichts. Was es meinte, tut der Baum — jeder Knoten trägt seinen eigenen Wert, in beide
Richtungen (ADR-019 §4).

**Warum die Operator-Rechte 1:1 und nicht über eine Abbildungstabelle?**
Weil beide dasselbe Format haben. Eine Tabelle wäre eine zweite Wahrheit über dieselbe
Berechtigung — und die erste, die jemand zu pflegen vergisst, macht eine Fläche still stumm.

**Warum ist das keine Vermischung von Backend und Frontend?**
Sie ist eine, und deshalb steht sie pro Fläche und ist nie Standard. Wer `Administration` wählt,
sagt: Diese Fläche ist Betriebswerkzeug, keine Kundenoberfläche. Eine öffentliche Website wählt
es nicht.

---

## 4. Blöcke erklären, was sie verlangen — offen

**Beschlossen, nicht umgesetzt.** Der Kompositions-Renderer kennt keine Claims. Er rendert jeden
Block, den ein Plugin liefert, und die Prüfung findet **im Block** statt — das Ergebnis ist ein
Kasten mit einer Fehlermeldung statt Abwesenheit.

Ein Block soll seine Claim-Anforderung deklarieren, wie eine Ansicht es längst tut
(`IHostSurfaceViewContributor.RequiredClaims`). Wer sie nicht erfüllt, sieht **keinen Block**.

Das ist mehr als Kosmetik: Eine Fehlermeldung verrät, dass es die Funktion gibt und wer sie
haben darf. Abwesenheit verrät nichts — dieselbe Begründung, aus der ein unsichtbarer Knoten mit
404 statt 403 beantwortet wird (ADR-019 §4).

**Warum es hier nicht mitkommt:** Blöcke werden ausschließlich im Client registriert
(`registerBlock` aus `@callora/surface`). Der Host kennt nur ihre Ids, und
`SurfaceCompositionRenderer` bekommt deshalb bloß ein `blockIsAvailable(blockId)`. Es gibt
serverseitig nichts, wogegen eine Claim-Anforderung geprüft werden könnte.

Der Baustein braucht drei Schritte über drei Repos und gehört als eigener geplant:

1. `registerBlock({ requiredClaims })` im npm-SDK `@callora/surface`,
2. einen Weg, diese Deklaration serverseitig zu kennen (Manifest oder Beitrags-Vertrag),
3. die Filterung im Renderer — an derselben Stelle wie `blockIsAvailable`, nicht daneben.

Bis dahin bleibt es beim heutigen Verhalten: Der Block rendert und meldet selbst, was ihm fehlt.
Mit §2 ist das für den Betreiber-Fall entschärft — auf einer `Administration`-Fläche bringt er
seine Rechte jetzt mit, also meldet der Block nichts mehr.

---

## 5. Konsequenzen

### 5.1 Positiv

* Ein Betreiber ohne Identity-Plugin kann seine eigene Fläche benutzen — das war bisher unmöglich.
* Was der Host tut, steht künftig auch dran: Die Verzweigung auf `IdentityPluginId` wird zur Wahl.
* Blöcke, die jemand nicht bedienen darf, erscheinen nicht mehr kaputt, sondern gar nicht.

### 5.2 Negativ / Kosten

* **Operator-Rechte sind gröber als Surface-Claims.** `communication.calls.read` gilt
  plattformweit; ein Surface-Claim gälte für eine Fläche. Wer beides mischt, muss wissen, dass
  die Operator-Seite immer die weitere ist.
* Ein SuperAdmin sieht auf einer `Administration`-Fläche alles, was irgendein Plugin je verlangt.
* Ein Block ohne Deklaration bleibt für alle sichtbar. Das ist die verträgliche Richtung — ein
  stiller Standard „verlangt alles" ließe bestehende Layouts leer werden.

### 5.3 Migration

`20260809…_RenameAccessModeToAuthentication` bildet ab, ohne Verhalten zu ändern:

| vorher | nachher |
| --- | --- |
| `Public` | `Public` |
| `Mixed` | `Public` |
| `Authenticated` **mit** `IdentityPluginId` | `SurfaceIdentity` |
| `Authenticated` **ohne** | `Administration` |

Die letzten beiden Zeilen bilden ab, was der Host schon tat.

---

## 6. Abgrenzung

* **Gewährte Claims bleiben** (`GrantedClaims`): Sie geben JEDEM etwas, `Administration` gibt
  einem Operator seine eigenen Rechte. Beides zusammen ist zulässig.
* **Die Sichtbarkeit je Knoten bleibt** (`RequiredClaims`, ADR-019 §4). Diese Achse sagt, woher
  Claims kommen; jene, welche verlangt werden.
* **Der Identity-Provider bleibt an der Wurzel** (ADR-017 §5.2). `SurfaceIdentity` benennt nur,
  dass er gilt.
