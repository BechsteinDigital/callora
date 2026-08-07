# ADR-022 — Wer die Adressen unterhalb einer Fläche bestimmt

**Status:** Accepted
**Datum:** 2026-08-08
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-014 — Surface-Engine (§5.1 Definition, §5.2 Verantwortung)
* ADR-019 — Surfaces als Baum
* ADR-021 — Der Workspace steht in der öffentlichen Route

---

## 1. Kontext

### 1.1 Der Befund

Die öffentliche Auflösung nimmt das **längste passende Pfadpräfix**. Was dahinter stand, gab sie
nie heraus — und niemand fragte danach. `/test/blub/gibtsnicht` antwortete mit **200 und dem
Inhalt von `/test/blub`**.

Das ist die teuerste Fehlerklasse: kein Statuscode, kein Log, kein Hinweis. Der Aufrufer sucht
überall außer beim Routing. Derselbe Mechanismus hatte kurz zuvor einen unaufgelösten
`/api/`-Pfad mit einer gerenderten Flächenseite beantwortet.

Dabei kam heraus, dass **zwei Stellen** denselben Restpfad berechneten
(`SurfaceRenderEndpoints.StripPrefix` und die Prüfung selbst) und **beide** am Zeichen statt an
der Segmentgrenze verglichen: `/test/blubber` galt als `/test/blub` plus `ber`.

### 1.2 Warum es nicht mit einer Regel getan ist

Ein Restpfad ist nicht per se falsch. Zwei Fälle stehen sich gegenüber:

* Eine **Website**: Der Baum ist die Wahrheit. `/portal/kunden/tippfehler` gibt es nicht, wie in
  jedem Shop.
* Eine **Anwendung**: `/raeume/abc123` ist keine Seite, sondern eine Instanz, die zur Laufzeit
  entsteht. Sie kann gar nicht als Knoten angelegt worden sein.

### 1.3 Der erste, verworfene Ansatz

Zuerst leitete die Regel das Verhalten aus dem **Renderweg** ab: Wer ein eigenes Server-Template
(`index.njk`) mitbringt, darf Unterpfade deuten. Das koppelt zwei unabhängige Dinge —
*Darstellung* und *Adressierung*. Ein Template ist kein Router; und eine Anwendung mit
History-Routing braucht durchgereichte Unterpfade gerade **ohne** Server-Template. Der
Zusammenhang war zufällig.

---

## 2. Entscheidung

Die Fläche trägt eine eigene Achse, `SurfaceRouting`:

| Wert | Bedeutung |
| --- | --- |
| `Tree` (Standard) | Der Seitenbaum ist die Wahrheit. Ein Pfad, der keinem Kind entspricht, ist **404**. |
| `Application` | Die Anwendung deutet ihre Unterpfade selbst. |

**Standard ist `Tree`.** Wer nichts sagt, bekommt 404 statt einer fremden Seite. Ein stiller
Default in die andere Richtung liefert unter jedem Tippfehler 200 mit dem Inhalt der Wurzel —
genau der Fehler, der diese Achse nötig gemacht hat.

**Nicht vererbt.** Jeder Knoten beantwortet die Frage für sich. Ein geerbtes `Application`
machte still jeden Tippfehler unter einem ganzen Teilbaum zu einer 200.

**Der Renderweg bleibt derselbe.** Eine Anwendung benutzt dieselben njk-Templates und dieselben
Inseln wie jede andere Fläche und steht damit unter demselben Theme. Daran hängt White-Label:
Eine Anwendung, die ihre eigene Optik mitbrächte, fiele auf.

---

## 3. Warum so

**Warum eine eigene Achse und nicht `SurfaceType`?**
Der Typ ist beschreibend und frei — auch ein Plugin trägt dort ein, was es für richtig hält
(`PluginSurfaceDefinition.SurfaceType`). Aus einem freien Wort auf das Routingverhalten zu
schließen hieße, jeden Wert zu kennen, den morgen jemand erfindet. Heute steht dort überall
pauschal `"spa"`, ohne dass es je etwas bedeutet hätte.

**Warum nicht aus dem Vorhandensein einer Komposition?**
Das deckte den Composer-Fall ab, aber nur solange veröffentlicht ist. Eine Seite mit Entwurf und
ohne veröffentlichte Version verhielte sich wie eine Anwendung — und die Adressierung darf nicht
davon abhängen, ob jemand gerade auf „Veröffentlichen" geklickt hat.

**Warum darf `/surface/render` durch?**
Der Direktaufruf löst über den **Host** auf; sein eigener Pfad ist keine Adresse innerhalb der
Fläche. Ohne die Ausnahme wäre er selbst der Rest, den niemand beansprucht.

---

## 4. Konsequenzen

### 4.1 Positiv

* Ein Pfad, den niemand bedient, antwortet mit 404 statt mit fremdem Inhalt.
* Der Restpfad wird an **einer** Stelle berechnet (`SurfaceRouteRemainder`), an der
  Segmentgrenze.
* Instanz-Adressen bleiben möglich, ohne dass jemand Knoten für sie anlegt — und behalten das
  Theme der Fläche.

### 4.2 Negativ / Kosten

* Eine Fläche, deren Anwendung Deep-Links erwartet, muss auf `Application` gestellt werden.
  Bestandsflächen wandern auf `Tree`, weil das der überwiegende Fall ist; wer eine Anwendung
  betreibt, stellt sie um. Ausdrücklich, statt dass eine Migration es für ihn annimmt.
* Ein unbekannter Wert im API-Aufruf ist ein 400, kein stiller Rückfall auf `Tree` — sonst
  beantwortete eine gemeinte Anwendung jeden Instanzpfad mit 404, und niemand erführe, dass der
  Wert nie angekommen ist.

### 4.3 Migration

`20260807230110_AddSurfaceRouting` fügt `workspace_surfaces.routing` hinzu, nicht nullable, mit
Vorgabe `"Tree"` — **nicht** `""`: Der Wertkonverter liest den Spaltentext als Enum-Namen, und
EFs Vorgabe für eine nicht-nullable Zeichenkette wäre kein gültiger Name gewesen. Jede
Bestandszeile käme beim ersten Lesen als Ausnahme zurück.

---

## 5. Offen

Ein Layout, das für **alle Instanzen** einer Anwendung gilt (der Konferenzraum, nicht ein
einzelner Raum), hat noch keine Heimat. Der Composer bindet Layouts an `(workspace, surface)`;
eine Instanz hat keinen eigenen Knoten. Shopwares Gegenstück ist die Erlebniswelt vom Typ
„Produktseite" samt Datenzuordnung — die Bindungsarten dafür gibt es bereits
(`SurfaceBlockBinding` mit `source: context`), die Zuordnung fehlt.
