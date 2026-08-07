# ADR-021 — Der Workspace steht in der öffentlichen Route

**Status:** Accepted
**Datum:** 2026-08-07
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-014 — Surface-Engine (§5.2 Verantwortung, §5.3 Mehrere Surfaces pro Workspace)
* ADR-019 — Surfaces als Baum

> **Supersedes (teilweise):** Dieses ADR löst die Aussage ab, dass **der Workspace keine
> eigene Adresse hat** und jede Route ausschließlich einer Surface gehört (ADR-014 §5.2,
> im Code als „the workspace itself carries no address"). Der Workspace kann künftig einen
> **Host** tragen. Ein **Pfad** bleibt Sache der Surfaces — daran ändert sich nichts.

---

## 1. Kontext

### 1.1 Was galt

Nur eine Surface trug Host und Pfadpräfix. Beim Anlegen eines Workspaces entsteht
automatisch eine `default`-Surface; ohne weitere Angabe steht sie auf Host `null` und Pfad
`/`.

### 1.2 Der Befund

Beim vollständigen lokalen Durchlauf vor 0.9 wurden zwei Workspaces angelegt. Beide
bekamen eine `default`-Surface mit Host `null` und Präfix `/`. Damit beanspruchten **beide
die gesamte Origin**:

* `PublicRouteMatching.Score` vergibt für beide dieselbe Punktzahl.
* Die Auflösung entschied still und reproduzierbar für denselben Workspace.
* Der zweite Workspace war **unerreichbar** — ohne Hinweis in der Administration, im Log
  oder sonstwo. Jede Anfrage lieferte 200 mit dem Inhalt des anderen.

Das ist kein Randfall, sondern der Normalfall: Er tritt ein, sobald jemand einen zweiten
Workspace anlegt, ohne vorher eine Domain zu besitzen.

### 1.3 Was fehlte

Ein Adressraum, der die Workspaces voneinander trennt, ohne eine Domain vorauszusetzen.

---

## 2. Entscheidung

**Eine Basis-URL kann einen Workspace bezeichnen oder eine Surface. Eine Seite kann nie
eine Basis-URL sein.**

Daraus folgen drei Fälle, in dieser Vorrangfolge:

| Fall | Host | Pfad |
| --- | --- | --- |
| Surface trägt einen Host | der der Surface (oder geerbt) | die Surface-Kette |
| Sonst: Workspace trägt einen Host | der des Workspaces | die Surface-Kette |
| Sonst | keiner | **Workspace-Schlüssel** + Surface-Kette |

Konkret:

```
kein Host        →  host.de/<workspace>/<surface>/<kind>/…
Workspace-Host   →  kunde.de/<surface>/<kind>/…
Surface-Host     →  portal.kunde.de/<kind>/…
```

**Die Surface gewinnt gegen den Workspace**, weil sie das speziellere Signal ist: Wer
`portal.kunde.de` auf eine Surface legt, meint diese Surface, auch wenn der Workspace
`kunde.de` trägt.

**Benennt ein Host den Workspace bereits, entfällt sein Segment im Pfad.** Es zweimal zu
sagen wäre keine Unterscheidung, sondern eine Wiederholung.

---

## 3. Warum so

**Warum ein Host am Workspace und nicht eine dritte Ebene im Surface-Baum?**
Weil eine Basis-URL beides bezeichnen kann und der Workspace bereits die Einheit ist, die
Daten zusammenhält. Eine künstliche Wurzel-Surface „für die Domain" wäre ein Knoten ohne
Inhalt, den jeder Anwender erklärt bekommen müsste.

**Warum das Workspace-Segment statt einer Pflicht zur Domain?**
Eine Installation ohne eigene Domain ist der Normalzustand beim ersten Start. Eine Pflicht
hätte den ersten zweiten Workspace blockiert; das Segment kostet nichts und ist jederzeit
gegen einen Host austauschbar.

**Warum keine Kollisionswarnung statt einer Strukturänderung?**
Eine Warnung hätte den Zustand sichtbar gemacht, nicht behoben — der Anwender stünde vor
der Wahl zwischen zwei Workspaces mit derselben Adresse und keinem Weg heraus.

**Warum ändert das die bestehende Auflösung nicht?**
Host und Pfad werden weiterhin an genau einer Stelle bestimmt: `EffectiveSurface.From`.
Sowohl die Auflösung (`MatchSurfaceByPublicRouteAsync`) als auch die Ausgabe an den
Renderpfad gehen durch sie. Eine zweite Stelle, die dasselbe entscheidet, gibt es nicht —
das war die Fehlerklasse, die den ganzen Durchlauf dominiert hat.

---

## 4. Konsequenzen

### 4.1 Positiv

* Zwei frisch angelegte Workspaces sind ohne jede Konfiguration unterscheidbar erreichbar.
* Der Weg von „läuft lokal" zu „läuft unter einer Domain" ist ein Feld, kein Umbau.
* Die Adresse ist aus Workspace-Schlüssel und Surface-Kette ablesbar, ohne die Datenbank
  zu befragen.

### 4.2 Negativ / Kosten

* **Bestehende URLs ändern sich.** Eine Surface ohne Host, die bisher unter `/` erreichbar
  war, liegt jetzt unter `/<workspace>/`. Das betrifft jede Installation, die ohne Domain
  betrieben wurde — vor 0.9 also die Entwicklungs- und Testinstanzen.
* Der Workspace-Schlüssel wird öffentlich sichtbar, wo kein Host gesetzt ist. Er ist ein
  technischer Schlüssel, kein Geheimnis; wer das nicht will, setzt einen Host.

### 4.3 Migration

`20260807220000_AddWorkspacePublicHost` fügt `workspaces.public_host` samt Index hinzu.
Nullable, kein Rückschreiben — bestehende Workspaces bekommen ihr Segment im Pfad und
sind damit sofort eindeutig erreichbar.

---

## 5. Abgrenzung

* **Der Workspace bekommt keinen Pfad.** Ein Pfad gehört einer Surface; ein Workspace mit
  Pfad wäre eine vierte Adressquelle für dieselbe Anfrage.
* **Der Tenant bleibt außen vor.** Er ist die Abrechnungs- und Isolationsachse, nicht die
  Adressachse (vgl. Tenant/Workspace/Surface-Semantik, ADR-014 §18).
* **Mehrere Hosts je Workspace** sind nicht vorgesehen. Wer das braucht, legt Surfaces mit
  eigenen Hosts an — dafür ist der Baum da.

---

## 6. Zusicherungen

`PublicRouteCarriesTheWorkspaceTests` deckt alle drei Fälle ab, einschließlich der
Kollision, die das ADR nötig gemacht hat: zwei `default`-Surfaces mit Präfix `/` und ohne
Host müssen unterschiedliche Pfade ergeben.
