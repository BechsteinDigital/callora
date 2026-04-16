# Engineering Rules

Diese Datei ist die zentrale technische Regelquelle fuer alle Agenten (Codex, Claude) und alle Beitragsarten.

## Mandatory Workflow
1. Lies diese Datei vor jeder Analyse, Planung, Implementierung und jedem Review.
2. Wende die Regeln strikt an.
3. Wenn eine User-Anforderung einer Regel widerspricht, fordere eine explizite Override-Entscheidung an.

## Architekturregeln (DDD)
1. DDD ist verpflichtend: `Domain`, `Application`, `Infrastructure` sauber trennen.
2. Keine Schichtverletzungen und keine zyklischen Abhaengigkeiten.
3. `Infrastructure` bleibt internes Implementierungsdetail.
4. Keine Produktlogik im Telephony-Kern, wenn sie in Host/Plugins gehoert.

## Codequalitaet und Wartbarkeit
1. Keine verschachtelten Typen: keine `class` in `class`, keine `interface` in `class`, keine `record` in `class`.
2. Maximal 1000 Zeilen pro Datei, deutlich kleiner bevorzugt.
3. Kleine, fokussierte Klassen und Funktionen bevorzugen.
4. Klare Fehlerbehandlung, kein stilles `catch`.
5. Oeffentliche APIs mit XML-Dokumentation versehen.

## TDD und Tests
1. TDD-orientiert arbeiten: erst Verhalten/Testfall definieren, dann Implementierung.
2. Jede funktionale Aenderung braucht passende Tests.
3. Tests muessen Verhalten absichern, nicht nur Codepfade beruehren.
4. Keine DONE-/Compliance-Claims ohne technische Evidenz.

## Thread-Safety und Performance
1. Thread-Safety by design in allen Runtime-Pfaden.
2. CPU- und RAM-schonende Implementierung als Default.
3. Keine unnoetigen Allokationen in Hotpaths.
4. Konkurrenzzugriffe explizit absichern (Locks, lockfreie Strukturen, Immutability je nach Kontext).

## Umsetzungshinweise
1. Bevorzuge DI statt harter Instanziierung.
2. Bevorzuge klare Contracts zwischen Host, SDK und Plugins.
3. Halte Dateien und Typen in einem Zustand, der einfache Reviews erlaubt.
