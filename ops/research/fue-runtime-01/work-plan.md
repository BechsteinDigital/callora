# FuE-Arbeitsplan FUE-RUNTIME-01

- Stand: 2026-07-15
- Status: Arbeitsentwurf fuer BSFZ-Fachbeschreibung und Stundenklassifikation

| AP | Technische Arbeit | Nachweisziel | Status am 15.07.2026 |
|---|---|---|---|
| AP 0 | Baselines und reproduzierbare Fehlerszenarien fuer stale Chains, ALC-Pinning, Vertragskonflikte und parallele Workspace-Wechsel aufbauen | Ausgangszustand, Versuchsaufbau und verbleibende Wissensluecke dokumentiert | teilweise vorhanden |
| AP 1 | Varianten fuer Shared/Private Type Identity und Contract-Versionen entwerfen und in mehreren Plugin-Load-Contexts testen | Load-/Reject-/Restart-Matrix ohne doppelte Public-Typen | teilweise vorhanden |
| AP 2 | Stabile Core-Proxys sowie versionierte Decoration-/Contribution-Registries als Varianten entwickeln | atomare, deterministische Kettenwahl ohne Halten deaktivierter Instanzen | Prototyp vorhanden, Zielmodell offen |
| AP 3 | Symmetrischen Registration-, Drain- und Teardown-Lifecycle fuer Routen, Events, Decorators, Jobs, Contributor, Exports und Assets entwickeln | vollstaendige Deregistrierung und sichtbare Fehlerzustaende | teilweise vorhanden |
| AP 4 | Workspace-spezifische Availability- und Capability-Wechsel unter Parallelzugriff untersuchen | keine gemischten Zustaende und keine Cross-Workspace-Sichtbarkeit | teilweise vorhanden |
| AP 5 | Mindestens zwei fachlich verschiedene Referenzplugins als Generalisierungsprobe einsetzen | Nachweis, dass der Runtime-Mechanismus nicht Communication-spezifisch ist | teilweise vorhanden |
| AP 6 | Fault-Injection-, Last-, Soak-, Update- und Unload-Wiederholungsversuche ausfuehren | Hypothese bestaetigt, eingeschraenkt oder widerlegt; technische Grenzen dokumentiert | offen |

## Zuordnungsregel fuer Stunden

Eine Stunde darf nur einem AP zugeordnet werden, wenn die persoenliche Taetigkeit unmittelbar zur
technischen Fragestellung oder zur Auswertung eines kontrollierten Versuchs beigetragen hat.
Gemischte Arbeitsbloecke werden aufgeteilt. Ist eine belastbare Aufteilung nicht moeglich, bleibt
der gesamte Block ausserhalb des FuE-Nachweises.

## Noch festzulegen

- Enddatum und Zeitbedarf der offenen AP,
- eingefrorene Messziele nach AP 0,
- konkrete Experiment-IDs und Rohdatenablage,
- Entscheidung, ob April bis Juli ein durchgehendes Vorhaben oder Juli ein Folgevorhaben ist.

