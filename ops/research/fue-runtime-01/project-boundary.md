# Projektabgrenzung FUE-RUNTIME-01

- Stand: 2026-07-15
- Status: Arbeitsentwurf
- Vorlaeufiger Beginn: 2026-04-16
- Spaetestens eindeutig im Code erkennbar: 2026-04-26
- Ende: noch festzulegen

## Technische Aufgabe

Untersucht und experimentell entwickelt wird ein Runtime-Verfahren, das Pluginbeitraege in einer
laufenden ASP.NET-Core-Instanz je Workspace konsistent aktivieren, deaktivieren und aktualisieren
kann. Nach einem Zustandswechsel sollen neue Aufrufe genau eine vollstaendige Konfiguration sehen,
waehrend alte Aufrufe kontrolliert beendet werden. Anschliessend duerfen keine Routen, Listener,
Decorators, Jobs, Contributor, Exports, Assets oder Caches die deaktivierte Plugininstanz oder deren
collectible `AssemblyLoadContext` festhalten.

Gleichzeitig muessen gemeinsam genutzte Vertragsassemblies eine einheitliche Typidentitaet
behalten, waehrend plugin-private und versionsabweichende Abhaengigkeiten isoliert bleiben.

## Einbezogene FuE-Taetigkeiten

- reproduzierbare Baselines fuer ALC-Pinning, stale Decorator Chains, gemischte
  Workspace-Zustaende und Vertragskonflikte,
- Entwurf und Erprobung alternativer Registry-, Snapshot-, Proxy-, Drain- und
  Teardown-Verfahren,
- Konflikt- und Typidentitaetstests ueber mehrere Plugin-Load-Contexts,
- Lifecycle-Versuche fuer dynamische Routen, Events, Decorators und weitere Contributions,
- Konkurrenz-, Fault-Injection-, Wiederholungs- und Unload-Tests,
- technische Auswertung gescheiterter Varianten und daraus folgende Anpassungen,
- fachlich unterschiedliche Referenzplugins, soweit sie die Domaenenneutralitaet des
  Runtime-Verfahrens pruefen.

## Ausgeschlossene Taetigkeiten

- normale CRUD-, RBAC-, Login-, Billing-, Marketplace- und Verwaltungsfunktionen,
- UI, CSS, Designsystem, Shell- und Asset-Produktisierung,
- Standardintegration von Datenbanken, Redis, OpenTelemetry, OpenAPI oder CI,
- routinemaessige Security-, Datenschutz-, Release- und Betriebsarbeiten,
- reine Ordner-, Namespace-, Paket- und Repository-Umbauten,
- Communication-, Dialer- oder sonstige Fachfunktionen ohne experimentellen Runtime-Bezug,
- allgemeine Planung, Marktanalyse, Dokumentation oder Projektmanagement,
- reine Fehlerkorrekturen und Regressionstests nach bereits feststehendem Loesungsweg,
- nicht persoenlich geleistete Maschinen-, Build- oder KI-Agentenzeit.

## Vorlaeufige Beginnentscheidung

Der 16.04.2026 ist als Beginn nur vertretbar, wenn die damalige persoenliche Arbeit am
installierbaren Plugin-Scaffold bereits der hier beschriebenen technischen Runtime-Frage diente.
Der Commit vom 26.04.2026 belegt die workspace-faehige Plugin-Control-Plane eindeutiger.

Vor Einreichung muss der Inhaber bestaetigen:

- ob sachlich zusammengehoerige Vorarbeiten bereits vor dem 16.04.2026 existierten,
- ob die April-Arbeiten auf eigene Rechnung und eigenes technisches Risiko erfolgten,
- ob der enge Runtime-Gegenstand bereits im April verfolgt wurde oder erst im Juli als
  eigenstaendiges Folgevorhaben begann.

Da beide derzeit vertretbaren Startpunkte nach dem 31.12.2025 liegen, veraendert diese offene
Abgrenzung die vorlaeufige Berechnung der 20-Prozent-Gemeinkostenpauschale nicht. Sie muss dennoch
sachlich richtig angegeben werden.

