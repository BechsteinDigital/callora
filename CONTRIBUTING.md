# Mitmachen

Fehlerberichte, Vorschläge und Pull Requests sind willkommen. Was hier steht, ist das,
was du vorher wissen solltest — vor allem, was das Repository beim Bauen erzwingt.

## Der Weg

```bash
git clone https://github.com/BechsteinDigital/callora.git
cd callora
dotnet restore Callora.Host.sln
dotnet build Callora.Host.sln -p:SkipAdminFrontend=true   # ohne Node deutlich schneller
dotnet test  Callora.Host.sln
```

Arbeite auf einem Branch, nicht auf `main`, und öffne einen Pull Request. Ein Baustein,
der fertig ist, wird committet — Sammelcommits über mehrere Themen sind später nicht mehr
lesbar.

## Was der Build erzwingt

Dieses Repository verschiebt viel von dem, was anderswo ein Review leistet, in den Build.
Das ist Absicht: Ein Gate, das zubeißt, bevor jemand hinsieht, kostet niemanden Zeit mit
Diskussionen über Konventionen.

| Gate | Was passiert, wenn du es reißt |
|---|---|
| `TreatWarningsAsErrors` | Jede Compiler- oder Analyzer-Warnung bricht den Build |
| **CAL0001–0004** | Zugriff über die `[CalloraInternal]`-Grenze, Ableiten davon, undokumentierte Vertragsfläche, roher String statt Extension-Point-Konstante |
| **PublicAPI-Baselines** | Jede Änderung der öffentlichen Fläche muss als Diff in `PublicAPI.Unshipped.txt` erscheinen |
| **ArchitectureRulesTests** | Verschachtelte Typen, mehrere Typen pro Datei, `partial`, über 1000 Zeilen, falsche Schichtrichtung |
| **Documentation-Tests** | Dokumentierte Konfigurationsschlüssel, die es nicht gibt; Vorgabewerte, die vom Code abweichen; tote Links; unvollständiger Extension-Point-Katalog |
| **Landkarte** | `docs/REPO_MAP.md` ist nicht mehr aktuell |

Die Baselines in `ArchitectureRulesTests` **dürfen nur schrumpfen**. Ein Eintrag ohne
Verstoß lässt den Test ebenfalls scheitern — wer eine Datei aufräumt, streicht ihren
Eintrag, und niemand kann neue hinzufügen.

Die verbindlichen Regeln stehen in [`ENGINEERING_RULES.md`](ENGINEERING_RULES.md) und
[`CODE_STRUCTURE_RULES.md`](CODE_STRUCTURE_RULES.md). Wer sich orientieren will, fängt
bei [`CLAUDE.md`](CLAUDE.md) und der [Landkarte](docs/REPO_MAP.md) an.

## Was ein Pull Request mitbringt

- **Tests für die Verhaltensänderung.** Nicht Codepfade berühren — Verhalten absichern.
- **Doku im selben Zug**, wenn sich etwas ändert, das dokumentiert ist. Veraltete
  Dokumentation ist schlimmer als keine: Sie klingt plausibel, und jemand plant danach.
- **Den Beleg.** „Tests grün" heißt: Der Lauf ist gemacht und die Ausgabe steht dabei.

Kommentare erklären hier den **Befund**, nicht die Zeile — nicht „setzt das Timeout auf 2
Sekunden", sondern warum es einmal bei 5 stand und was dabei herauskam. Wer eine Zeile
schreibt, die auf einer teuren Erkenntnis beruht, schreibt die Erkenntnis dazu. Das ist der
Grund, warum man sich in diesem Repository zurechtfindet.

## Developer Certificate of Origin

Beiträge laufen über den **[DCO](https://developercertificate.org/)**: eine Zeile im
Commit.

```bash
git commit -s -m "fix(surfaces): ..."
```

Das erzeugt `Signed-off-by: Dein Name <deine@mail>` und bedeutet: Du bestätigst, dass du
das Recht hast, diesen Beitrag unter der Lizenz des Projekts einzubringen. Kein Vertrag,
keine Rechteabtretung, keine Unterschrift per E-Mail.

**Warum kein CLA.** Ein Contributor License Agreement überträgt dem Projekteigentümer
Rechte, die über die Lizenz hinausgehen — typischerweise, um später relizenzieren zu
können. Das ist für den, der beiträgt, ein einseitiges Geschäft, und es hält Leute ab: Wer
einen Tippfehler in der Doku korrigieren will, unterschreibt dafür keinen Vertrag. Callora
steht unter Apache-2.0 und soll dort bleiben; damit braucht es die Rechte nicht, die ein
CLA einsammelt. Der DCO leistet, was tatsächlich nötig ist — die Herkunft eines Beitrags
nachvollziehbar zu machen.

Dein Copyright bleibt bei dir.

## Plugins

Ein Plugin gehört nicht in dieses Repository. Es baut gegen `Callora.Plugin.Sdk` und lebt
in seinem eigenen Repository — auch die, die wir selbst ausliefern
([ADR-020](docs/adr/ADR-020-repo-schnitt-und-paketgrenzen.md)). **Ein Plugin darf beliebig
lizenziert sein, auch proprietär**; Apache-2.0 verlangt davon nichts.

Der Einstieg steht in
[Build your first Callora plugin](docs-site/guides/getting-started/your-first-plugin.md).

## Lizenz

Mit deinem Beitrag stellst du ihn unter die [Apache-Lizenz 2.0](LICENSE), unter der auch
alles andere in diesem Repository steht.
