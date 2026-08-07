# ADR-019 — Surfaces als Baum (jede Surface kann eine Erlebniswelt haben)

**Status:** Accepted
**Datum:** 2026-08-07
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-014 — Surface-Engine (§5 Surfaces, §6.1 Access Modes, §14 Public Routing)
* ADR-015 — Surface-Template-Engine
* ADR-017 — Surface-Identität und Session-Transport (§5.2 Identity-Zuweisung)
* Spec `2026-08-06-admin-sdk-und-surface-composer-design` (§7 Surface Composer) — intern, `callora-ops`

> **Supersedes (teilweise):** Dieses ADR löst **ADR-014 §5.1 „Definition"** und **§5.3
> „Mehrere Surfaces pro Workspace"** ab. Dort war eine Surface eine flache Liste je
> Workspace, und jede trug ihre Zugangsdaten vollständig selbst. Sie bilden künftig einen
> **Baum**, in dem nur die Wurzel den Zugang definiert und Kinder ihn erben. Die
> **Verantwortungsliste aus §5.2 bleibt gültig** — sie beschreibt jetzt, was eine
> *Wurzel* trägt und was ein Kind überschreiben kann. Die **Access Modes aus §6.1 bleiben
> unverändert**, sie gelten nur zusätzlich je Knoten.

---

## 1. Kontext

### 1.1 Was heute gilt

Eine Surface ist eine Zugangsfläche innerhalb eines Workspaces — Callora-Gegenstück zum
Shopware-SalesChannel. Sie trägt Domain oder Pfadpräfix, Zugriffspolitik, Locale sowie
Template-, Theme- und Identity-Zuweisung. Ein Workspace hat N Surfaces auf gemeinsamen
Daten (ADR-014 §5.3).

Die öffentliche Auflösung läuft über **Host plus längstes Pfadpräfix**: Jede aktive Surface
wird gegen die Anfrage geprüft, die spezifischste gewinnt
(`ResolveSurfaceByPublicRouteAsync` → `PublicRouteMatching.Score`).

Der Kompositions-Renderer holt das Layout über `GetPublishedAsync(workspaceKey,
surfaceKey)` — **ohne Pfad**.

### 1.2 Was fehlt

**Es gibt genau ein Layout je Surface.** Eine Website mit Startseite, Über uns und Kontakt
ist damit nicht baubar: Man bräuchte drei Surfaces, jede mit eigenem Host oder Pfadpräfix,
eigener Zugriffspolitik und eigener Theme-Zuweisung. Für drei Seiten derselben Website ist
das offensichtlich falsch.

Bemerkenswert ist, **wie nah das System schon dran ist**:

* Der Pfad innerhalb der Surface wird bereits ermittelt (`StripPrefix`), geht in den
  Render-Kontext und an die Data-Contributors — nur nicht in die Layout-Auswahl.
* Die Routen-Auflösung ist bereits präfixbasiert und würde einen Baum ohne Änderung
  bedienen: `/portal` und `/portal/partner` als zwei Surfaces lösen heute schon korrekt
  auf, die spezifischere gewinnt.

Was fehlt, ist nicht das Routing, sondern die **Aussage, dass diese beiden zusammengehören**
— und mit ihr Vererbung, Navigation und die Möglichkeit, eine Struktur im Editor zu bauen,
statt sie über einzelne Zugangsflächen zusammenzustückeln.

---

## 2. Entscheidung

**Surfaces bilden einen Baum.** Ein Surface-Knoten hat optional einen Elternknoten
innerhalb desselben Workspaces.

Ein Knoten ist entweder:

* **Anwendungswurzel** — kein Elternteil. Trägt den Zugang verpflichtend: Host oder
  Pfadpräfix, Access Mode, Theme, Identity-Provider. Eine Wurzel ist das, was ADR-014 §5.1
  eine Surface nannte: Website, Dialer, Agent Desktop, Kundenportal.
* **Kind** — hat einen Elternteil. **Erbt** dessen Zugang und überschreibt nur, was es
  eigenes braucht. Ein Kind ist das, was Shopware eine Kategorie nennt.

**Jeder Knoten kann ein Layout haben** — die Erlebniswelt. Der Composer baut sie; ein Knoten
ohne Layout rendert wie bisher aus dem Template.

Der Workspace bleibt, was er ist: die **Datenklammer**. Ein Mandant kann mehrere Anwendungen
auf denselben Daten betreiben, und genau dafür gibt es mehrere Wurzeln je Workspace.

### 2.1 Warum nicht eine vierte Ebene

Erwogen und verworfen wurde, unter der Surface eine eigene Ebene „Seite" einzuziehen:
Workspace → Surface → Seite → Layout.

Das hätte **zwei Baumarten** nebeneinander gestellt — Surfaces flach, Seiten hierarchisch —
und die Frage nur verschoben, wann etwas das eine und wann das andere ist. Vor allem aber
ignoriert es, dass die Surface-Auflösung den Baum bereits kann. Eine zweite Hierarchie zu
bauen, während die erste ungenutzt daneben liegt, ist eine Ebene, die sich nicht verdient.

### 2.2 Was das gegenüber Shopware weiterträgt

Shopware kennt Kategorien nur im Shop. Weil eine Callora-Wurzel auch ein Dialer oder ein
Agent Desktop sein kann, bekommt **jede Anwendungsart** eine gegliederte Struktur, die der
Kunde selbst baut und im Editor befüllt — nicht nur der Verkaufskanal.

---

## 3. Vererbung

Ein Kind erbt vom nächsten Vorfahren, der den jeweiligen Wert setzt; gesetzt wird bis zur
nächsten **Wurzel** und nicht darüber hinaus.

| Eigenschaft | Wo gesetzt | Vererbung |
|---|---|---|
| Host / Pfadpräfix | Wurzel verpflichtend, Kind ergänzt sein Segment | Der Pfad eines Kindes ist der des Elternteils plus eigenes Segment |
| Access Mode | Jeder Knoten | Geerbt, überschreibbar in **beide** Richtungen |
| Theme | Wurzel verpflichtend | Geerbt, überschreibbar |
| Locale | Optional | Geerbt, überschreibbar |
| Template | Optional | Geerbt, überschreibbar |
| Layout | Optional je Knoten | **Nicht** geerbt — siehe §3.2 |
| Identity-Provider | **Nur Wurzel** | Nicht überschreibbar — siehe §4 |

### 3.1 Der Access Mode ist in beide Richtungen überschreibbar

`/portal` `Authenticated` mit `/portal/impressum` `Public` ist genauso legitim wie `/portal`
`Mixed` mit `/portal/partner` `Authenticated`. Beides kommt vor, und eine Regel „nur
verschärfen" würde den ersten Fall erzwingen, indem man das Impressum an eine eigene Wurzel
hängt — womit es ein anderes Theme und eine andere Navigation bekäme.

Was das braucht, ist **Sichtbarkeit in der Verwaltung**: Ein geerbter Wert muss als geerbt
erkennbar sein, ein überschriebener als überschrieben. Ein Kind, das den Modus lockert, ist
eine Entscheidung — sie darf nicht wie ein Standardwert aussehen.

### 3.2 Ein Layout wird nicht vererbt

Erwogen: Ein Knoten ohne eigenes Layout rendert das des Elternteils.

Verworfen. Bei einer 50-Seiten-Struktur klingt Vererbung nach Ersparnis, führt aber dazu,
dass eine Seite ohne eigenes Layout aussieht wie ihre Elternseite — also *inhaltlich falsch*,
nicht *leer*. Wer eine Kategorie anlegt und vergisst, ihr eine Erlebniswelt zu geben, bekommt
dann eine Seite, die plausibel aussieht und das Falsche zeigt. Eine leere Seite ist eine
Frage, eine falsch befüllte ist ein Fehler, den niemand meldet.

Für den Fall, dass viele Knoten dieselbe Darstellung brauchen, ist die Antwort ein
**Template** — das wird vererbt und ist genau dafür da.

---

## 4. Identität und Sichtbarkeit

Hier laufen zwei Dinge zusammen, die getrennt bleiben müssen.

**Authentifizierung — wer ist das? — gehört zur Wurzel.** Nur eine Wurzel setzt einen
Identity-Provider (ADR-017 §5.2). Damit ist die Session-Grenze deckungsgleich mit der
Anwendungsgrenze: Wer sich an einer Wurzel anmeldet, ist im ganzen Baum darunter angemeldet,
und nirgends sonst. Ein Kind, das einen eigenen Anmeldebereich bräuchte, ist keine Kategorie
mehr — es ist eine eigene Anwendung und gehört zur Wurzel gemacht.

Die Alternative — Realm je Knoten — wurde verworfen: Sie ließe eine Anmeldung mitten im Baum
enden, ohne dass die URL das verrät, und die Frage „bin ich hier angemeldet?" hinge dann an
der Vererbungskette statt an der Anwendung.

**Autorisierung — was darf diese Person sehen? — gehört zum Knoten.** Ein Knoten kann
verlangen, dass der Besucher bestimmte Claims mitbringt; ohne sie erscheint er weder in der
Navigation noch beim direkten Aufruf.

> **Nicht das Operator-RBAC.** `BackendRbacRole` und `BackendRbacRoleGrant` regeln, wer im
> Admin was darf; ihre Konsumenten sind Admin-API, MCP-Tools und Plugin-Routen. Ein
> Portal-Besucher ist kein Operator und hat keine Backend-Rolle. Die Sichtbarkeit eines
> Knotens prüft die Claims des `SurfaceCaller` aus ADR-017 — dieselbe Identität, die das
> Rendering ohnehin trägt. Beide Systeme über einen Kamm zu scheren hieße, Besuchern
> Backend-Rollen zu geben.

**Kommunikation zwischen Wurzeln bleibt der Shared Context.** `ISharedContextService` mit
seinen Ankern (`subject`, `conversation`) ist genau dafür gebaut und wird von diesem ADR
nicht berührt: Der Agent Desktop sieht denselben Anruf wie das Kundenportal, ohne dass beide
im selben Baum hängen müssten.

---

## 5. Navigation

Die Navigation eines Knotens sind seine Kinder — bis zur nächsten Wurzel und nicht darüber
hinaus. Damit stehen Website und Dialer nie in derselben Navigation, obwohl beide Surfaces
sind.

Ein Knoten erscheint in der Navigation, wenn er aktiv ist, sichtbar für den aufrufenden
Besucher (§4) und nicht ausdrücklich ausgeblendet. Ein Knoten ohne Layout bleibt navigierbar
— er ist dann eine Gliederungsebene, kein Fehler.

Damit fällt die in ADR-014 §5.2 der Surface zugeschriebene Verantwortung „Navigation" aus dem
Baum ab, statt separat gepflegt zu werden.

---

## 6. Was sich technisch ändert

1. **`WorkspaceSurface` bekommt `ParentSurfaceId`** (nullable, innerhalb desselben
   Workspaces) und eine Positionsangabe für die Reihenfolge unter Geschwistern.
2. **Die Zugangs-Auflösung liest die Vererbungskette.** `ResolveSurfaceByPublicRouteAsync`
   findet weiterhin den spezifischsten Knoten; der aufgelöste Snapshot trägt danach die
   **effektiven** Werte — geerbt, wo der Knoten selbst nichts sagt.
3. **`PublicPathPrefix` eines Kindes ist relativ.** Der volle Pfad entsteht aus der Kette.
   Ein Kind trägt `partner`, nicht `/portal/partner`: Sonst müsste beim Verschieben eines
   Teilbaums jeder Nachfahre umgeschrieben werden.
4. **Layouts hängen weiter am Surface-Schlüssel.** `ISurfaceLayoutSource.GetPublishedAsync`
   bleibt unverändert — was sich ändert, ist nur, dass es mehr Surfaces gibt. Das ist der
   Grund, aus dem dieses Modell dem vierstufigen vorgezogen wurde.
5. **Der Composer bekommt eine Baum-Ansicht** statt eines Textfelds für den Layout-Schlüssel:
   Knoten anlegen, verschieben, Erlebniswelt geben oder wegnehmen.

Nicht geändert werden: die Access-Mode-Semantik (§6.1), der Shared Context, das
Operator-RBAC, der Kompositions-Renderer und das Theme-Modell.

---

## 7. Folgen

**Migration.** Bestehende Surfaces werden Wurzeln — `ParentSurfaceId` bleibt null, und alles
verhält sich wie bisher. Vorhandene Layouts bleiben, wo sie sind.

**Zyklen.** Ein Knoten darf nicht sein eigener Vorfahre sein. Das ist beim Setzen zu prüfen,
nicht beim Auflösen: Ein Zyklus, der erst beim Rendern auffällt, ist eine Endlosschleife im
Anfragepfad.

**Löschen.** Was mit den Kindern eines gelöschten Knotens geschieht, ist eine
Produktentscheidung und hier bewusst offen: Sie an den Großelternknoten zu hängen ändert
stillschweigend URLs, sie mitzulöschen verliert Layouts. Die Verwaltungsoberfläche muss
danach fragen.

**Tiefe.** Es gibt keine technische Grenze, wohl aber eine Anzeigegrenze. Eine sehr tiefe
Struktur macht die Vererbungskette schwer nachvollziehbar; das ist ein Fall für eine Warnung
in der Verwaltung, nicht für eine Sperre.

---

## 8. Umsetzungsschnitt

| # | Baustein | Liefert |
|---|---|---|
| 1 | `ParentSurfaceId` + Position, Zyklusprüfung, Migration | Der Baum existiert, alles Bestehende bleibt Wurzel |
| 2 | Effektive Auflösung mit Vererbungskette | Ein Kind erbt Theme, Access Mode, Locale, Template |
| 3 | Relativer Pfad und Ketten-Zusammensetzung | Teilbäume sind verschiebbar, ohne Nachfahren umzuschreiben |
| 4 | Navigation aus dem Baum, begrenzt auf die Wurzel | Die Fläche bekommt ihre Gliederung |
| 5 | Sichtbarkeit je Knoten über Caller-Claims | „Kann" statt „muss" — der Teil, der §4 real macht |
| 6 | Baum-Ansicht im Composer | Struktur bauen und Erlebniswelten vergeben |

Bausteine 1–3 sind die Grundlage; 4–6 verdienen sich einzeln.
