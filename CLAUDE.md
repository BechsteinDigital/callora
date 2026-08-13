# Arbeiten in diesem Repository

Für KI-Agenten und für Menschen am ersten Tag. Was hier steht, steht sonst nirgends —
alles andere ist verlinkt statt wiederholt.

## Was das hier ist

Das **Framework**, nicht das Produkt: ein Satz paketierbarer Bibliotheken. Der lauffähige
Prozess und die Zusammenstellung einer Distribution liegen im separaten Repository
`callora-production`. `src/Host/Dev` ist die einzige lauffähige Zusammenstellung hier und
ausdrücklich kein Produkt — sie existiert, damit ein `F5` funktioniert und damit auffällt,
wenn jemand `AddCalloraHost` oder die Reihenfolge der Modulaufrufe bricht.

Der Kern ist **domänenneutral**: Er weiß nichts von Telefonie, Terminen oder Kunden. Er weiß,
wie Plugins geladen, isoliert, versorgt und ausgeliefert werden. Alles Fachliche kommt aus
Plugins — die in **eigenen Repositories** leben (ADR-020). `custom/static-plugins/` ist leer
und bleibt es.

Landkarte: [`docs/REPO_MAP.md`](docs/REPO_MAP.md) — erzeugt von `scripts/build-repo-map.sh`,
per CI aktuell gehalten.

## Regeln, die hier gelten

Vor jeder Aufgabe zu lesen, nicht zu überfliegen:

- [`ENGINEERING_RULES.md`](ENGINEERING_RULES.md) — DDD-Schichtung, Testpflicht, Thread-Safety
- [`CODE_STRUCTURE_RULES.md`](CODE_STRUCTURE_RULES.md) — ein Typ pro Datei, keine verschachtelten
  Typen, keine `partial`, ≤1000 Zeilen, Ordnerstruktur

Sie sind nicht Absichtserklärung: `ArchitectureRulesTests` erzwingt sie im Testlauf, und die
dort geführten Baselines **dürfen nur schrumpfen**. Ein Eintrag ohne Verstoß lässt den Test
ebenfalls scheitern — die Liste kann also nur kleiner werden.

## Befehle

```bash
dotnet build Callora.Host.sln                       # baut die Admin-SPA mit (vue-tsc + vite)
dotnet build Callora.Host.sln -p:SkipAdminFrontend=true   # ohne Node, deutlich schneller
dotnet test  Callora.Host.sln
bash scripts/build-repo-map.sh                      # Landkarte neu erzeugen
```

Die Frontend-Suiten laufen eigenständig und sind **nicht** Teil von `dotnet test`:

```bash
cd src/Administration/Resources/app/administration && npm ci && npm run test
cd src/Surface.Rendering/Resources/app/surface     && npm ci && npm run test
```

## Was du nicht lesen solltest

Diese Dateien fressen Kontext und enthalten nichts, was du nicht anderswo schneller bekommst:

| Pfad | Warum nicht |
|---|---|
| `src/Core/Infrastructure/Persistence/Migrations/*.Designer.cs` | Generiert, ~1.900 Zeilen pro Datei, ~40.000 gesamt |
| `src/*/PublicAPI.Unshipped.txt` | Generierte Baseline, allein Core hat 6.879 Zeilen |
| `**/package-lock.json`, `**/node_modules/` | — |
| `src/Surface.Rendering/Resources/nunjucks.js` | Fremdcode (gebündelte UMD-Distribution) |

Willst du wissen, was die öffentliche Fläche enthält, lies die Typen — nicht die Baseline.

## Wenn Dokumentation und Code sich widersprechen

**Der Code gewinnt, und die Dokumentation ist dann ein Fehler — keine Randnotiz.** Sie gehört
im selben Zug korrigiert.

Der Grund steht in der Historie: Das Betriebs-Runbook beschrieb ein manuelles SQL zur
Job-Wiederherstellung, das es seit Einführung der Lease-Logik nicht nur nicht mehr brauchte,
sondern das laufende Jobs ein zweites Mal ausführte. Veraltete Dokumentation ist schlimmer als
gar keine: Sie klingt plausibel, und man glaubt ihr, bis man sie gegen den Code prüft.

Dagegen laufen Gates unter `tests/Callora.Core.Tests/Documentation/`. Kommt eine Aussage dazu,
die veralten kann, kommt ein Test dazu.

**Bei Widerspruch zwischen einem Issue und einem ADR führt das Issue.** Zweifel vorher klären,
dann eine neue ADR mit `Supersedes`-Block schreiben — nicht die alte still umschreiben.

## Wie hier gearbeitet wird

- **Branch → PR.** Nicht auf `main` committen.
- **Abgeschlossene Bausteine sofort committen**, nicht sammeln. Ein Commit pro Baustein liest
  sich später; ein Sammelcommit nicht.
- **Gefundene Mängel werden gefixt, nicht notiert.** Wer beim Arbeiten über einen Fehler
  stolpert, nimmt ihn mit. Ein Follow-up-Eintrag ist die zweitbeste Lösung und meistens die
  letzte, die jemand sieht.
- **Keine DONE-Meldung ohne Beleg.** Tests grün heißt: der Lauf ist gemacht und die Ausgabe
  steht dabei.
- Commits und Code-Kommentare sind auf Deutsch oder Englisch — beides kommt vor, richte dich
  nach der Datei, in der du bist.

## Kommentare

Der wertvollste Teil dieses Repositories sind seine Kommentare, und sie folgen einer eigenen
Regel: **Sie erklären den Befund, nicht die Zeile.** Nicht „setzt das Timeout auf 2 Sekunden",
sondern warum es einmal bei 5 stand und was dabei herauskam. Nicht „registriert den Dienst",
sondern warum er ausgeschrieben komponiert wird statt vom Container geraten.

Wer eine Zeile schreibt, die auf einer teuren Erkenntnis beruht, schreibt die Erkenntnis dazu.
Das ist der Grund, warum man sich hier zurechtfindet.

## Fallen, die Zeit gekostet haben

- **Dev-Stack:** Die Frontdoor läuft auf **8080**, nicht auf 5000. Port 5000 direkt liefert
  unter `/admin/` einen Redirect-Loop — das ist Absicht, kein Fehler.
- **Host-Builds brauchen einen gestoppten Dev-Container** (NuGet-`obj`-Race im Bind-Mount →
  NETSDK1064).
- **NuGet-Cache bei lokalen Paketen:** Gleiche Versionsnummer (`0.1.0-local`) gibt den **alten**
  Paketinhalt zurück. Reihenfolge: `pack` → Feed synchronisieren → **Cache löschen** → Bundle
  bauen. Ein Test gegen einen warmen Cache prüft die vorige Version.
- **EF-Wertkonverter greifen nicht bei NULL.** Eine nullable Spalte umgeht den Konverter
  stillschweigend — beim Mapping mitdenken.
- **Plugin-Migrationen:** Die Design-Time-Factory scheitert, weil `Callora.Core` nicht im
  Output liegt. In Plugin-Repositories läuft das Tooling über das Testprojekt als Startprojekt.
- **Vite `emptyOutDir`** räumt beim Bauen mehr weg, als man erwartet — Ausgabepfade prüfen,
  bevor man ihn anschaltet.

## Wenn du etwas veränderst, das andere bauen

Die öffentliche Fläche ist per Baseline nachverfolgt (`PublicAPI.Unshipped.txt`). Eine Änderung
daran ist kein Versehen, sondern ein reviewbarer Diff — `dotnet build` erzwingt das. Die
Governance-Analyzer CAL0001–0004 bewachen dieselbe Grenze zur Bauzeit; ihre Regeln stehen in
[`docs-site/reference/analyzer-rules.md`](docs-site/reference/analyzer-rules.md).

Für die Teilmenge, die mit `[CalloraExtensible]` markiert ist, kommt eine zweite Stufe dazu:
`src/Core/ExtensionSurface.txt`. Sie darf sich ändern — aber nicht beiläufig. Der Grund steht in
#283: Eine Signatur bekam einen Parameter, `contractVersion` blieb stehen, und ein Plugin aus einem
fremden Repository ließ sich danach nicht mehr laden. Die `PublicAPI`-Baseline enthielt die
Änderung, nur fragt niemand beim Nachziehen, ob sie fremde Bauwerke bricht. Genau diese Frage
stellt das Gate, und beantworten muss sie ein Mensch: Ist es ein Bruch, steigt vorher
`contractVersion`; ist es rein additiv, genügt das Erneuern der Datei.
