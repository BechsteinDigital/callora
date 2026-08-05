# Technischer Nachweisindex FUE-RUNTIME-01

- Stand: 2026-07-15
- Status: Rekonstruktionsgrundlage, kein Ersatz fuer persoenliche Stundenaufzeichnungen

## Bewertungslogik

| Stufe | Bedeutung |
|---|---|
| A | Code und konkrete Tests belegen eine technische FuE-nahe Untersuchung oder Variante. |
| B | Code oder technische Dokumente belegen den Gegenstand, die FuE-Abgrenzung bleibt gemischt. |
| C | Nur Dokument-/Metadatenspur oder ueberwiegend Routinearbeit; nicht ohne Zusatznachweis ansetzen. |

Ein Commit beweist Inhalt und Zeitpunkt einer Aenderung, aber weder die persoenlich geleistete Zeit
noch automatisch deren Foerderfaehigkeit. Bei KI-unterstuetzter Entwicklung zaehlt nur die eigene
Zeit fuer technische Konzeption, Versuchsanordnung, Steuerung, Review, Fehleranalyse und
Auswertung.

## Evidenz nach Datum

| Datum | AP | Quelle | Technische Aussage | Stufe | Abgrenzungsrisiko |
|---|---|---|---|---|---|
| 16.04.2026 | AP 0 | `f9f34d4` | installierbares Host-Plugin-Scaffold als fruehe Grenzprobe | B | Voice- und Scaffold-Anteil ist keine FuE-Zeit |
| 17.04.2026 | AP 0/AP 3 | `docs/monitoring/PLUGIN_LIFECYCLE_SLO.md`, Plattformtickets | Lifecycle- und Betriebszustaende wurden technisch strukturiert | C | Dokumentation und Planung allein reichen nicht |
| 18.04.2026 | AP 3/AP 4 | `docs/adr/ADR-008-workspace-activation-scope.md`, `ADR-009-code-first-extension-wiring.md`, `PLUGIN_CONTRACT_V1.md` | Workspace-Aktivierung und Extension-Wiring wurden als technische Grenzen entworfen | C | Architekturentscheidung kann routinemaessig sein |
| 26.04.2026 | AP 0/AP 3/AP 4 | `a01c850` | workspace-faehige Control-Plane, Vertragspruefung, Install/Update/Rollback und Lifecycle-Tests | B | sehr grosser Mischcommit mit UI, RBAC, Persistence und Standardintegration |
| 12.07.2026 | AP 3/AP 5 | `b5a178f`, `4b8d21d`, `e5d02bc`, `bac667e` | Contract-Schicht, zweites Referenzplugin und Lifecycle-/Relay-Haertung | B | viel Produkt- und Enterprise-Standardentwicklung am selben Tag |
| 13.07.2026 | AP 1 | `9e50524` | gemeinsame Vertragsassemblies ueber getrennte Plugin-Load-Contexts | A | Implementierung darf nicht pauschal komplett angesetzt werden |
| 13.07.2026 | AP 3 | `1e6ffdc`, `9a41e68` | sichtbare `Faulted`-/`UnloadFailed`-Zustaende und dynamische Plugin-Routen | A/B | Controller-Modell selbst ist Stand der Technik |
| 13.07.2026 | AP 4 | `16acac3` | Entitlement und gewuenschte Workspace-Aktivierung technisch getrennt | B | Produktlogikanteil abgrenzen |
| 14.07.2026 | AP 2 | `33f5a0d` | generische Service-Decoration mit Test-Prototyp | A | aktueller Resolve-Ansatz ist noch nicht der stabile Proxy |
| 14.07.2026 | AP 3 | `3cc5957`, `7823fad` | dynamische Event-/Routing-Beitraege und Fehler-/Overlay-Varianten | A/B | generischer Eventbus allein ist Routineentwicklung |
| 14.07.2026 | AP 0/AP 3 | `docs/PHASE0_HAERTUNG_AUDIT_2026-07-14.md` | fehlende Unload-Verifikation und Lifecycle-Luecken konkret identifiziert | B | reine Audit-/Dokumentationszeit nicht pauschal ansetzen |
| 14.07.2026 | AP 1-AP 4 | `docs/CALLORA_ZIELARCHITEKTUR_DOMAENENNEUTRALE_PLUGIN_PLATTFORM_REV2.md` | stale Chains, Core-Proxy, Type Identity und vollstaendiger Teardown als offene Zielprobleme formuliert | B | Nordstern enthaelt zahlreiche nicht foerderfaehige Produktphasen |
| 15.07.2026 | AP 3/AP 4 | `0ab1959` | ALC-Unload-Pruefung, Lifecycle-State-Mapping und zentraler Availability-Evaluator mit Tests | A/B | Commit enthaelt zusaetzlich Job-, Persistence- und Security-Haertung |
| 15.07.2026 | AP 1/AP 5 | `e12bb76`, `a16274e`, `43bbddb` | Communication-Abstractions und Foundation-Tier als Domaenenneutralitaetsprobe | B | grosser Anteil ist strukturelle Migration und Build-Nachlauf |

## Besonders belastbare Code-/Testspuren

- `src/Hosting/Application/Plugins/SharedContractAssemblyRegistry.cs`
- `src/Hosting/Application/Plugins/PluginAssemblyLoadContext.cs`
- `src/Hosting/Application/Plugins/RuntimePluginHost.cs`
- `src/Hosting/Application/Plugins/AssemblyLoadContextUnload.cs`
- `src/Host/Backend/Application/Plugins/PluginAvailabilityEvaluator.cs`
- `src/Hosting/Application/Plugins/PluginServiceDecoration.cs`
- `src/Host/Backend/Infrastructure/Http/PluginApiEndpointDataSource.cs`
- `tests/Callora.Host.Backend.Tests/Hosting/SharedContractAssemblyRegistryTests.cs`
- `tests/Callora.Host.Backend.Tests/Application/Plugins/AssemblyLoadContextUnloadTests.cs`
- `tests/Callora.Host.Backend.Tests/Application/Plugins/PluginAvailabilityEvaluatorTests.cs`
- `tests/Callora.Host.Backend.Tests/Application/Extensibility/PluginServiceDecorationTests.cs`
- `tests/Callora.Host.Backend.Tests/Infrastructure/Http/PluginApiEndpointDataSourceTests.cs`

## Was noch fehlt

- zeitnahe persoenliche Stundenaufzeichnungen fuer die Vergangenheit,
- explizite Experiment-IDs mit Hypothese, Varianten, Rohdaten und Ergebnis,
- reproduzierbare Konkurrenztests fuer atomare Workspace-Wechsel,
- vollstaendiges Contribution-Inventar vor und nach Teardown,
- gemessene ALC-Unload-, Leistungs- und Wiederholungsresultate,
- Bestaetigung, welche Commit-Arbeit persoenliche FuE-Arbeit und welche Agenten-/Routinearbeit war.
