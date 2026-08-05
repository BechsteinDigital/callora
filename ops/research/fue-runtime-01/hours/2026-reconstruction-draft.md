# Rekonstruktionsentwurf Eigenleistungen 2026

- FuE-Vorhaben: `FUE-RUNTIME-01`
- Stand: 2026-07-15
- Status: **ENTWURF — nicht ungeprueft einreichen oder unterschreiben**
- Methode: konservative Rueckrechnung aus Git-, Test- und Dokumentenspuren

## Ergebnis

| Kategorie | Stunden | Behandlung |
|---|---:|---|
| konservativer Kern mit Code-/Testspuren | 45,0 | nur nach persoenlicher Bestaetigung jeder Zeile verwendbar |
| Dokumentationsreserve April | 5,0 | vorerst nicht geltend machen; nur mit zusaetzlichem persoenlichem Nachweis hochstufen |
| gesamte untersuchte Rekonstruktionsspanne | 50,0 | keine automatische Antragssumme |

Die empfohlene vorlaeufige Antragssumme ist damit **nicht 50, sondern hoechstens 45 Stunden**.
Auch diese 45 Stunden sind zu reduzieren, falls einzelne Taetigkeiten nicht persoenlich geleistet,
nicht erinnerlich oder ueberwiegend Routine-/Produktarbeit waren.

## Tagesbezogener Kern

| Datum | KW | AP | Persoenliche FuE-Taetigkeit im Rekonstruktionsmodell | Stunden | Hauptbelege | Status |
|---|---:|---|---|---:|---|---|
| 16.04.2026 | 16 | AP 0 | Installierbares Plugin-Scaffold als erste technische Grenzprobe zwischen Host, Contract und Plugin-Paket untersucht | 2,0 | `f9f34d4` | ENTWURF |
| 26.04.2026 | 17 | AP 3 | Install-/Update-/Rollback- und Vertragspruefpfade der workspace-faehigen Plugin-Control-Plane prototypisch umgesetzt und Lifecycle-Tests ausgewertet | 3,5 | `a01c850` | ENTWURF |
| 26.04.2026 | 17 | AP 4 | Workspace-Aktivierung, Entitlement, Capability-Abhaengigkeiten und Zustandsuebergaenge im Prototyp untersucht | 2,5 | `a01c850` | ENTWURF |
| 12.07.2026 | 28 | AP 5 | Communication-Vertraege mit Dialer als zweitem fachlichen Verbraucher auf Domaenenneutralitaet und Contract-Grenzen geprueft | 3,0 | `b5a178f`, `4b8d21d` | ENTWURF |
| 12.07.2026 | 28 | AP 3 | Plugin-Lifecycle, asynchrone Relays und Handler-Abmeldung anhand konkreter Fehlerbilder nachgearbeitet und getestet | 3,0 | `e5d02bc`, `bac667e` | ENTWURF |
| 12.07.2026 | 28 | AP 4 | Workspace-bezogene Pluginzustands- und Datenisolationspfade technisch geprueft; Standard-Persistence-Anteil ausgeschlossen | 2,0 | `290b861`, `817ebc2` | ENTWURF |
| 13.07.2026 | 29 | AP 1 | Shared-Contract-Type-Identity ueber getrennte collectible Plugin-Load-Contexts implementiert und Konfliktfaelle getestet | 4,0 | `9e50524` | ENTWURF |
| 13.07.2026 | 29 | AP 3 | Fehlerzustaende `Faulted`/`UnloadFailed` und dynamische Routingbeitraege mit Laufzeitaktualisierung untersucht | 3,0 | `1e6ffdc`, `9a41e68` | ENTWURF |
| 13.07.2026 | 29 | AP 4 | Gewuenschte Workspace-Aktivierung von Entitlement und effektiver Verfuegbarkeit getrennt und Verhalten geprueft | 2,0 | `16acac3` | ENTWURF |
| 14.07.2026 | 29 | AP 2 | Generische Service-Decoration als Prototypvariante implementiert, Reihenfolge und Resolve-Verhalten getestet | 3,0 | `33f5a0d` | ENTWURF |
| 14.07.2026 | 29 | AP 3 | Dynamische Event- und Routingbeitraege einschliesslich Fehler- und Host-Overlay-Varianten untersucht | 4,0 | `3cc5957`, `7823fad` | ENTWURF |
| 14.07.2026 | 29 | AP 3 | Plugin-eigene Schema-/Lifecyclepfade mit Deinstallations- und Real-Postgres-Varianten geprueft; reine Persistence-Arbeit ausgeschlossen | 2,0 | `233ecbc`, `bc87498`, `f244b42` | ENTWURF |
| 14.07.2026 | 29 | AP 0 | Unload-, Availability- und Lifecycle-Luecken technisch analysiert und den weiteren Versuchsschnitt abgeleitet | 3,0 | `PHASE0_HAERTUNG_AUDIT_2026-07-14.md`, REV2 | ENTWURF |
| 15.07.2026 | 29 | AP 3 | Tatsachliches ALC-Unloading ueber Weak-Reference-/Collection-Pruefung und sichtbare Lifecycle-Zustaende implementiert und getestet | 3,0 | `0ab1959` | ENTWURF |
| 15.07.2026 | 29 | AP 4 | Effektive Plugin-Verfuegbarkeit aus Runtime-, Entitlement-, Workspace- und Capability-Faktoren zentralisiert und getestet | 2,0 | `0ab1959` | ENTWURF |
| 15.07.2026 | 29 | AP 1/AP 5 | Communication-Abstractions und System-Plugin-Grenze als Shared-Type-/Domaenenneutralitaetsprobe umgesetzt und Integrationsfolgen geprueft | 3,0 | `e12bb76`, `a16274e`, `43bbddb` | ENTWURF |
|  |  |  | **Kernsumme** | **45,0** |  |  |

## Noch nicht ansetzbare Reserve

| Datum | KW | AP | Technische Spur | Stunden | Warum vorerst Reserve |
|---|---:|---|---|---:|---|
| 17.04.2026 | 16 | AP 0/AP 3 | Lifecycle-SLO und technische Plattformtickets strukturiert | 2,0 | Dokument-Metadaten belegen keine persoenliche FuE-Dauer; moeglicherweise reine Planung |
| 18.04.2026 | 16 | AP 3/AP 4 | Workspace-Aktivierung, Plugin-Contract und Code-first Extension-Wiring konzipiert | 3,0 | technische Konzeption ist erkennbar, aber ohne weiteren Nachweis nicht sicher von Routinearchitektur abgrenzbar |
|  |  |  | **Reservesumme** | **5,0** |  |

## Wochenplausibilitaet

| ISO-Woche | Kernstunden | Reserve | Maximal nach Rekonstruktion | Gesetzliche 40-h-Grenze unterschritten |
|---:|---:|---:|---:|---|
| 16 | 2,0 | 5,0 | 7,0 | ja |
| 17 | 6,0 | 0,0 | 6,0 | ja |
| 28 | 8,0 | 0,0 | 8,0 | ja |
| 29 | 29,0 | 0,0 | 29,0 | ja |

Pflegezeiten und Kundenzeiten werden nicht im FuE-Stundennachweis erfasst. Fuer die interne
Plausibilitaet ist zu beruecksichtigen, dass 25 Stunden Pflege pro Woche und rund 30 Stunden
Kundenarbeit pro Monat hinzukamen. Besonders KW 29 bildet damit eine aussergewoehnliche
Gesamtbelastung ab. Das passt zur geschilderten Arbeit ueber dem koerperlichen und mentalen Limit,
ist aber kein Ersatz fuer die Bestaetigung der konkreten FuE-Stunden.

## Vorlaeufige finanzielle Einordnung

Unter den noch zu pruefenden Annahmen `100 EUR je anerkannter Eigenleistungsstunde`,
`20 Prozent Gemeinkostenpauschale` und `35 Prozent KMU-Satz` ergibt sich:

| Stundenbasis | Foerderfaehige Eigenleistung | mit 20-%-Pauschale | Zulage bei 35 % | Zulage bei 25 % |
|---|---:|---:|---:|---:|
| 45,0 Stunden Kern | 4.500 EUR | 5.400 EUR | 1.890 EUR | 1.350 EUR |
| 50,0 Stunden inklusive Reserve | 5.000 EUR | 6.000 EUR | 2.100 EUR | 1.500 EUR |

Die 50-Stunden-Zeile ist nur eine Sensitivitaet. Sie darf erst verwendet werden, wenn die
Reservestunden eigenstaendig belegt und als unmittelbare FuE-Taetigkeit bestaetigt sind.

## Bestaetigungsfragen je Zeile

1. Habe ich diese Taetigkeit an diesem Kalendertag selbst ausgefuehrt?
2. Ist die Stundenzahl eine ehrliche Erinnerung beziehungsweise durch zeitnahe Quellen gedeckt?
3. War die Arbeit unmittelbar technisch-experimentell und nicht nur Planung, Routine oder Review
   generierten Codes?
4. Ist die Zeit frei von Kundenarbeit, Pflege, Pausen, Maschinenlaufzeit und autonomen
   Agentenlaeufen?
5. Kann ich den technischen Gegenstand und das Ergebnis bei einer Rueckfrage erklaeren?

Nur wenn alle Fragen mit Ja beantwortet werden, darf der Eintrag bestaetigt werden.
