# Callora: Vorprüfung zur Forschungszulage

- Stand: 2026-07-15
- Antragsteller im Prüfmodell: Einzelunternehmen des Inhabers
- Gegenstand: Callora als domänenneutrale, zur Laufzeit erweiterbare .NET-Plattform
- Ergebnisart: technische und steuerliche Vorprüfung, keine Rechts- oder Steuerberatung

## 1. Kurzurteil

**Callora ist für die Forschungszulage interessant, aber nicht als Gesamtprojekt.**

Der aussichtsreichste, klar abgrenzbare FuE-Gegenstand ist die experimentelle Entwicklung einer
**zur Laufzeit rekonfigurierbaren, workspace-spezifischen Plugin-Control-Plane für ASP.NET Core**.
Der technische Kern ist die noch nicht durch Standardlösungen abgedeckte Kombination aus:

- plugin-privaten und zugleich kontrolliert geteilten Assembly-Typidentitäten,
- workspace-spezifischer Aktivierung und effektiver Verfügbarkeit,
- dynamischer Service-Dekoration über langlebige Core-Proxys,
- dynamischer Registrierung und vollständigem Rückbau von Routen, Listenern, Jobs,
  Contributors, Exports und UI-Beiträgen,
- nachweisbarem Assembly-Unloading ohne Altinstanzen oder Cross-Workspace-Leaks,
- konsistentem Verhalten während paralleler Requests und Aktivierungswechsel.

Das ist wesentlich enger als „eigenes Shopware/Symfony für .NET“ und wesentlich präziser als
„Shopware for Voice“. Nur diese klar definierte technische Aufgabe sollte als ein FuE-Vorhaben
beschrieben werden. Produktisierung, Standard-CRUD, Marketplace, Billing, normale UIs,
Dokumentation, Security-Baseline und gewöhnliche Refactorings sind getrennt zu führen.

| Prüfaspekt | Vorläufiges Urteil | Begründung |
|---|---|---|
| Anspruch des Einzelunternehmens | hoch | Eigenleistungen eines Einzelunternehmers sind ausdrücklich vorgesehen. |
| Neuartigkeit des eng geschnittenen Runtime-Vorhabens | mittel | OSGi und .NET-Pluginbibliotheken sind starke Vorarbeiten; offen bleibt nur die konkret nachzuweisende ASP.NET-/Workspace-Gesamtsemantik. |
| Technisches Risiko | gut darstellbar | ALC-Pinning, Typidentität, stale Decorator Chains, Race Conditions und Scope-Leaks können den Ansatz scheitern lassen. |
| Planmäßigkeit | gut herstellbar | Architektur, Audits, Tests und Migrationsphasen liefern eine Basis; ein eigener FuE-Arbeitsplan fehlt noch. |
| Ganz Callora als FuE | schwach | Ein großer Anteil ist routinemäßige Plattform- und Produktentwicklung. |
| Rückwirkender Nachweis bisheriger Stunden | derzeit kritisch | Git und Tests belegen Arbeiten, aber nicht automatisch die eigenhändig geleistete FuE-Zeit. |

**Empfehlung: GO für die Vorbereitung eines BSFZ-Antrags**, sofern Projektbeginn, Träger,
KMU-Status, De-minimis-Rahmen und Stundenbelege vor Einreichung geklärt werden. Für „Callora
insgesamt“ lautet die Empfehlung **NO-GO**.

## 2. Geprüfte Grundlage

### 2.1 Repository- und Dokumentenprüfung

Im Arbeitsstand wurden 8.926 Markdown-Dateien inventarisiert:

- 6.091 Dateien im vendorten Shopware-Quellbaum,
- 2.752 Dateien in `node_modules` außerhalb des Shopware-Baums,
- 83 übrige Projekt-, Regel-, Betriebs-, Template- und Portaldokumente.

Die 83 nicht vendorten beziehungsweise nicht aus Abhängigkeiten stammenden Dateien wurden
inhaltlich geprüft. Der Shopware- und Abhängigkeitsbestand wurde als Fremdmaterial getrennt
behandelt, über relevante Begriffe systematisch durchsucht und bei einschlägigen Architekturtexten
vertieft gelesen. Fremde Changelogs, Lizenztexte und Paket-READMEs sind keine Nachweise für
Calloras FuE-Inhalt.

Besonders relevant waren:

- [Frühe Targetstruktur für KI](../../Callora_Targetstruktur_fuer_KI.md)
- [Domänenneutrale Zielarchitektur REV2](../../docs/CALLORA_ZIELARCHITEKTUR_DOMAENENNEUTRALE_PLUGIN_PLATTFORM_REV2.md)
- [Plugin-Plattform-Zielarchitektur](../../docs/CALLORA_PLUGIN_PLATTFORM_ZIELARCHITEKTUR.md)
- [Gesamtvision](../../docs/CALLORA_GESAMTVISION.md)
- [Plattform-Umbau vom 12.07.2026](../../docs/CALLORA_PLATTFORM_UMBAU_2026-07-12.md)
- [Phase-0-Härtungs-Audit](../../docs/PHASE0_HAERTUNG_AUDIT_2026-07-14.md)
- [Quality Standards](../../docs/QUALITY_STANDARDS.md)
- [Zustands-Audit](../audit/2026-07-14-audit.md)
- [ADR-012: Ein-Core-Extensibility](../../docs/adr/ADR-012-single-core-extensibility.md)
- [ADR-013: Trusted In-Process](../../docs/adr/ADR-013-trust-model-trusted-in-process.md)

Wichtiges Nachweisrisiko: `docs/` ist im derzeitigen Repository ignoriert. Diese Dokumente sind
damit lokal wertvoll, aber ohne zusätzliche Sicherung keine robuste, versionierte
Entstehungshistorie. Der Git-Verlauf und `ops/` sind belastbarer, müssen aber um FuE-spezifische
Versuchsprotokolle und Zeitaufzeichnungen ergänzt werden.

### 2.2 Entwicklung des Gegenstands

| Zeitpunkt | Dokument-/Codebild | Bedeutung für die Abgrenzung |
|---|---|---|
| 14.–16.04.2026 | Voice-Plattform aus Engine, Host und Produktplugins; Repository-Split | Früher Projektkern war kommunikations- und produktzentriert. |
| 18.–26.04.2026 | Workspace-Aktivierung, Entitlements, Plugin-Control-Plane | Der technische Lifecycle-Gegenstand ist bereits erkennbar. |
| 12.07.2026 | API-first CPaaS, ALC-Runtime, Plugin-Daten, Jobs und Shells | Breite Plattformimplementierung; viel davon ist Standardentwicklung. |
| 13.–14.07.2026 | Shared Contracts, dynamische Routen, Business Events, Decoration, Plugin-Datenbanken | Generische Erweiterungsmechanismen werden konkret. |
| 14.07.2026 | REV2 definiert den Host ausdrücklich domänenneutral und Communication als Foundation-Plugin | Sachlich neue Zielschärfung; möglicher, aber nicht automatisch neuer FuE-Gegenstand. |
| 15.07.2026 | Git belegt Tiers, Runtime-Härtung und Communication-Abstractions-Extraktion | Beginn der konkreten Migration ist nachvollziehbar. |

Der Projektbeginn darf nicht allein auf das Datum von REV2 oder einen günstigen Commit gelegt
werden. Maßgeblich ist, wann mit den sachlich zusammengehörenden technischen Arbeiten tatsächlich
begonnen wurde. Die Unterlagen sprechen mindestens für zwei vertretbare Lesarten:

1. ein durchgehendes Vorhaben „workspace-fähige Plugin-Control-Plane“ seit April 2026 oder
2. ein abgrenzbares Folgevorhaben „domänenneutrale Runtime-Rekonfiguration“ ab Juli 2026.

Da beide möglichen Startpunkte nach dem 31.12.2025 liegen, wäre die neue 20-Prozent-Pauschale für
Gemein- und Betriebskosten grundsätzlich in beiden Lesarten relevant. Trotzdem muss die sachlich
richtige Lesart dokumentiert werden. Falls gleichartige Vorarbeiten schon vor 2026 in einem anderen
Repository oder Auftrag begonnen haben, ist dies gesondert zu bewerten.

### 2.3 Für den Antrag relevante Dokumentationsdrift

Die Markdown-Gesamtschau zeigt nicht nur eine Weiterentwicklung, sondern auch parallel gültig
wirkende, sachlich überholte Aussagen:

| Ältere Aussage | Aktueller Stand | Folgerung für den Antrag |
|---|---|---|
| Callora ist primär eine Voice-Plattform aus Engine, Host und Voice-Plugins. | REV2 definiert ein domänenneutrales Framework; Communication ist ein Foundation-Plugin. | Nicht mit „Shopware for Voice“ oder einer VoIP-Produktbeschreibung beantragen. |
| `admin` ist die globale feste Basisrolle. | Der aktuelle Stand trennt `SuperAdmin` global und `Admin` workspace-spezifisch. | RBAC ist ohnehin kein FuE-Kern und sollte nur als Randbedingung erscheinen. |
| Plugin-Lifecycle kennt nur `Installed`, `Active`, `Inactive`. | Runtime und aktuelle Architektur kennen auch `Faulted` und `UnloadFailed`. | Das technische Risiko mit sichtbaren Fehlerzuständen anhand des aktuellen Modells beschreiben. |
| Telemetrie, Audit und Entitlement-Backend fehlen noch. | Tickets, Quality Standards und Code weisen große Teile als umgesetzt aus. | Keine bereits erledigte Standardarbeit als zukünftige FuE-Arbeit darstellen. |
| Out-of-process Isolation wird als Ziel erwogen. | ADR-013 legt Trusted In-Process fest; Sidecar/IPC ist nur ein späterer Exit. | Keine Sandbox- oder Schadcode-Isolation beanspruchen. |
| UI-Assets und Templates werden in älteren Dokumenten teils mit abweichenden API- und Rollbackpfaden beschrieben. | REV2 priorisiert die Frameworkgrenzen; laufende Shell-/Asset-Arbeit ist Produktisierung. | UI- und Template-Arbeit aus dem FuE-Vorhaben ausschließen. |

Vor Einreichung sollte jedes verwendete interne Dokument entweder als „aktuell“, „historisch“ oder
„abgelöst durch …“ gekennzeichnet werden. Für den fachlichen Antrag gilt REV2 zusammen mit
ADR-012/ADR-013 und dem tatsächlich getesteten Code; ältere Dokumente dienen nur als
Entwicklungshistorie.

## 3. Rechtliche und steuerliche Vorprüfung für das Einzelunternehmen

### 3.1 Grundvoraussetzungen

Nach [§ 1 FZulG](https://www.gesetze-im-internet.de/fzulg/__1.html) können Steuerpflichtige mit
Einkünften aus Land- und Forstwirtschaft, Gewerbebetrieb oder selbständiger Arbeit
anspruchsberechtigt sein. Das schließt ein Einzelunternehmen grundsätzlich ein. Die
[BMF-Übersicht](https://www.bundesfinanzministerium.de/Web/DE/Themen/Steuern/Steuerliche_Themengebiete/Forschungszulage/forschungszulage.html)
bestätigt, dass die Zulage unabhängig von der Gewinnsituation beansprucht werden kann.

Für Callora müssen insbesondere folgende Tatsachen stimmen:

- Das Einzelunternehmen führt das FuE-Vorhaben auf eigene Rechnung und eigenes technisches Risiko
  durch oder ist für einen klar bezeichneten Teil tatsächlich Auftraggeber.
- Die geltend gemachten Eigenleistungen sind persönliche FuE-Arbeiten des Einzelunternehmers.
- Dieselben Aufwendungen werden nicht doppelt über andere Beihilfen oder Förderprogramme gefördert.
- Das Vorhaben hat nach dem 01.01.2020 begonnen.
- Es handelt sich nicht nur um Markteinführung oder das reibungslose Funktionieren eines bereits
  im Wesentlichen festgelegten Produkts.

[§ 2 FZulG](https://www.gesetze-im-internet.de/fzulg/__2.html) verlangt eine Zuordnung zu
Grundlagenforschung, industrieller Forschung oder experimenteller Entwicklung. Für Callora passt
am ehesten **experimentelle Entwicklung**. Das Gesetz verlangt eine genau definierte, unteilbare
technische Aufgabe, klare Ziele, Tätigkeiten und konkrete Kriterien zur Ergebnisbewertung.

### 3.2 BSFZ-Prüfkriterien

Die [BSFZ-Hinweise](https://www.bescheinigung-forschungszulage.de/hilfen-zur-antragstellung)
prüfen kumulativ:

1. **Neuartigkeit:** Gewinn neuer Kenntnisse oder neuartige Nutzung des Stands der Technik.
2. **Technisches Risiko/Unwägbarkeit:** Der Lösungsweg oder das Ergebnis darf nicht vorab sicher
   sein; ein rein wirtschaftliches Risiko genügt nicht.
3. **Planmäßigkeit:** technische Ziele, Arbeitspakete, Ressourcen, Meilensteine und prüfbare
   Ergebnisse müssen zusammenpassen.

Callora kann diese Kriterien nur mit dem engen Runtime-Projekt erfüllen. „Wir bauen ein modulares
Framework“ oder „Shopware für .NET“ ist dafür zu allgemein und durch vorhandene Frameworks zu nah
am Stand der Technik.

## 4. Empfohlenes FuE-Vorhaben

### 4.1 Arbeitstitel

**Experimentelle Entwicklung einer workspace-spezifisch rekonfigurierbaren Plugin-Control-Plane
mit deterministischem Teardown für ASP.NET Core**

### 4.2 Genau definierte technische Aufgabe

Zu entwickeln und experimentell nachzuweisen ist ein Runtime-Verfahren, das in einer laufenden
ASP.NET-Core-Instanz Pluginbeiträge je Workspace atomar aktivieren, deaktivieren und austauschen
kann. Die Änderung muss für neue Aufrufe sofort wirksam werden, während bereits laufende Aufrufe
kontrolliert beendet werden. Danach dürfen weder Routen, Listener, Decorators, Jobs, Exports,
Contributor, UI-Beiträge noch andere Referenzen die alte Plugininstanz oder ihren collectible
`AssemblyLoadContext` festhalten.

Gleichzeitig müssen:

- öffentliche Vertragsassemblies prozessweit eine konsistente Typidentität behalten,
- plugin-private, auch versionskonfliktäre Abhängigkeiten isoliert bleiben,
- die effektive Verfügbarkeit aus Runtime-Health, Entitlement, Workspace-Aktivierung,
  Tenant-/Workspace-Status und Capability-Abhängigkeiten konsistent abgeleitet werden,
- parallele Workspaces unterschiedliche Pluginzustände ohne gegenseitige Leaks sehen,
- Lifecycle-Fehler sichtbar und wiederherstellbar bleiben.

### 4.3 Technische Hypothese

Ein langlebiger Core-Proxy kann pro Aufruf aus einem unveränderlichen, versionierten
Registry-Snapshot eine workspace-spezifische Decorator- und Contribution-Kette bilden. Kombiniert
mit Quiescence-/Drain-Phasen, symmetrischer Deregistrierung und kontrollierter Shared-Type-Identity
kann so ein linearer Zustandswechsel erreicht werden, nach dem die alte Plugininstanz vollständig
entladbar ist.

Diese Hypothese ist nicht durch den aktuellen Code bewiesen. Insbesondere die bestehende
[PluginServiceDecoration](../../src/Hosting/Application/Plugins/PluginServiceDecoration.cs)
bildet die Kette beim Resolve und ist noch nicht das in REV2 beschriebene stabile Proxy-Modell.

### 4.4 Stand der Technik und verbleibende Wissenslücke

| Stand der Technik | Bereits gelöst | Nicht automatisch gelöst |
|---|---|---|
| [.NET-Pluginmodell mit `AssemblyLoadContext`](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) | Laden von Plugins, getrennte Abhängigkeiten, gemeinsames Contract-Paket | Workspace-spezifische Hot-Aktivierung, atomare Registry-Wechsel, vollständiger Teardown aller Host-Erweiterungspunkte |
| [McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins) | Shared Types, Abhängigkeitsisolation, Hot Reload, entladbare Plugins sowie MVC-/Razor-Integration | Einheitliche Live-Transaktion über bereits aufgelöste Services, dynamische Hostbeiträge und unterschiedliche Workspace-Zustände |
| [OSGi Service Layer](https://docs.osgi.org/specification/osgi.core/8.0.0/framework.service.html) und [Tracker](https://docs.osgi.org/specification/osgi.core/7.0.0/util.tracker.html) | Dynamischer Bundle-Lifecycle, Service Registry, Scopes, Ranking, Deregistrierung, Classloader-Kompatibilität und Tracker für konkurrierende Änderungen | Übertragung auf ASP.NET Core sowie ein konsistenter, workspace-selektiver Wechsel von DI-Decoration, Endpoints, Jobs, UI und collectible .NET-ALCs |
| [Orchard Core Tenants](https://docs.orchardcore.net/en/latest/reference/modules/Tenants/) und [Features](https://docs.orchardcore.net/en/main/reference/modules/Features/) | Multi-Tenancy, Feature-Profile, Enable/Disable mit Abhängigkeiten | Calloras Kombination aus per-Aufruf-Decoration, collectible ALCs und nachweisbarer Referenzfreiheit |
| Shopware-Plugin-/Decoration-Muster | Breite Erweiterbarkeit über Decoration, Events, Adapter und Plugins | Der vendorte Stand belegt bei Plugin-State-Wechseln einen Kernel-/Container-Rebuild und damit ein anderes Laufzeitmodell |
| Callora-Bestand | Shared Contracts, collectible ALCs, dynamische Routen, Events, Availability-Evaluator und Unload-Prüfung | Einheitlicher atomarer Lifecycle über alle Contribution-Arten und bereits aufgelöste Services unter Parallelzugriff |

Die Neuartigkeit darf daher nicht mit `AssemblyLoadContext`, Multitenancy, Plugininstallation,
Feature-Abhängigkeiten oder dem Decorator-Pattern allein begründet werden. Diese Bausteine sind
bekannt. Auch ein dynamischer Service-Locator, Service References, Scopes, Rankings und
Registrierungs-Tracker sind durch OSGi vorweggenommen. Forschungsrelevant ist nur die
überprüfbare Kombination der verbleibenden Laufzeitgarantien im konkreten ASP.NET-Core- und
Workspace-Modell. AP 0 muss zeigen, dass diese Lücke nicht durch eine routinemäßige Übertragung der
OSGi- oder McMaster-Muster geschlossen werden kann. Gelingt dieser Nachweis nicht, sinkt die
Förderchance deutlich.

### 4.5 Technische Unwägbarkeiten

1. **Stale Chains und ALC-Pinning:** Bereits aufgelöste Singletons oder Decorator-Ketten können
   Plugininstanzen weiter referenzieren und einen erfolgreichen Teardown unmöglich machen.
2. **Race Conditions:** Aktivierung, Deaktivierung und Requests können überlappen. Ohne geeignetes
   Epoch-/Snapshot-Verfahren kann ein Aufruf eine gemischte Konfiguration beobachten.
3. **Cross-Workspace-Leakage:** Die Assembly ist prozessweit geladen, der wirksame Pluginzustand
   aber workspace-spezifisch. Falsches Caching kann Beiträge im falschen Workspace sichtbar machen.
4. **Typidentität und Versionen:** Geteilte Contracts müssen im Default-Kontext identisch sein,
   während private Abhängigkeiten abweichende Versionen benötigen. First-loaded-wins kann
   unlösbare Konflikte oder Neustartzwang erzeugen.
5. **Teardown-Vollständigkeit:** Ein übersehener Eventhandler, laufender Request, Scheduler,
   Cache, Export oder UI-Eintrag genügt, um Altverhalten oder Speicherlecks zu erzeugen.
6. **Fehleratomarität:** Teilweise Aktivierung oder teilweise Deregistrierung kann weder als aktiv
   noch als inaktiv korrekt dargestellt werden.
7. **Leistung:** Der Registry-Lookup und die Kettenbildung pro Aufruf können die notwendige
   Laufzeitgarantie zwar herstellen, aber mit unvertretbarem Overhead.

Der Ansatz kann technisch scheitern, etwa wenn langlebige ASP.NET-Core-Objektgraphen keine sichere
Rekonfiguration ohne Prozess- oder Tenant-Kernel-Neustart zulassen. Gerade diese offene Frage ist
für die FuE-Einstufung wertvoll und darf nicht als bereits gelöst dargestellt werden.

### 4.6 Abgrenzung zum Trust- und Sandbox-Modell

Callora führt Plugins gemäß ADR-013 als vertrauenswürdigen In-Process-Code aus. Der kuratierte
Service Provider ist eine API-Grenze, aber keine Sicherheits-Sandbox. Das FuE-Vorhaben darf daher
**keine Isolation gegen bösartigen Plugin-Code** versprechen. Signaturen, Publisher-Prüfung,
Freigaben und Operator-Consent sind Governance und überwiegend routinemäßige Produktarbeit.

## 5. FuE-Arbeitsplan

Die Zeiträume und Personenmonate müssen vor dem BSFZ-Antrag mit den tatsächlichen Daten ergänzt
werden. Der folgende Schnitt trennt experimentelle Tätigkeiten von Produktisierung.

| AP | Technische Arbeit und Versuch | Prüfergebnis | Vorläufiger Status |
|---|---|---|---|
| AP 0 | Technische Baseline: reproduzierbare Szenarien für stale Decorators, ALC-Pinning, Vertragskonflikte und parallele Workspace-Aktivierung erzeugen; OSGi-Service-Reference-/Tracker- und McMaster-Ansätze als Vergleichsvarianten prüfen | Messbare Ausgangswerte, reproduzierte Fehler und dokumentierte verbleibende Lücke gegenüber den Vergleichsansätzen | teilweise vorhanden |
| AP 1 | Varianten für Shared/Private Type Identity und Contract-Versionen entwerfen; Konfliktmatrix mit mehreren Plugins experimentell ausführen | dokumentierte Load-/Reject-/Restart-Matrix ohne doppelte Public-Typen | teilweise vorhanden |
| AP 2 | Stabilen Core-Proxy und versionierte Decoration Registry als konkurrierende Prototypvarianten entwickeln | atomare Kettenwahl, deterministische Reihenfolge, kein Halten deaktivierter Instanzen | offen/teilweise |
| AP 3 | Einheitlichen Registration- und Teardown-Lifecycle für Routen, Events, Contributors, Jobs, Exports, Navigation und Assets entwickeln | vollständige symmetrische Deregistrierung, definierte Quiescence und sichtbare Fehlerzustände | teilweise vorhanden |
| AP 4 | Workspace-spezifische Availability- und Capability-Transitions unter Parallelzugriff untersuchen | keine gemischten Zustände oder Cross-Workspace-Sichtbarkeit in kontrollierten Konkurrenztests | teilweise vorhanden |
| AP 5 | Zwei fachlich verschiedene Referenzplugins mit Konflikt-, Fehler- und Updatevarianten aufbauen | Nachweis, dass der Mechanismus nicht Communication-spezifisch ist | offen |
| AP 6 | Fault-Injection-, Last-, Soak- und Wiederholungsversuche durchführen; gescheiterte Varianten und Grenzen bewerten | Hypothese bestätigt, eingeschränkt oder widerlegt; nachvollziehbarer technischer Abschluss | offen |

Reine Literaturrecherche, allgemeine Machbarkeitsbewertung und administratives Projektmanagement
sind nicht als FuE-Zeit anzusetzen. AP 0 ist nur insoweit förderfähig, wie konkrete technische
Versuche und Prototypen durchgeführt werden.

### 5.1 Vor Einreichung festzulegende Messgrößen

Die Zielwerte müssen aus einer dokumentierten Baseline abgeleitet und dann vor den
Lösungsversuchen eingefroren werden. Ungeprüfte Fantasiewerte sollten nicht in den Antrag.

| Messgröße | Vorgeschlagene Nachweismethode |
|---|---|
| Registry-Konsistenz | Konkurrenztest mit protokollierten Config-Epochen; jeder Aufruf sieht genau eine vollständige Epoche. |
| Workspace-Isolation | randomisierte Paralleltests mit mindestens zwei Workspaces und gegensätzlichen Aktivierungszuständen; null Fremdbeiträge. |
| Teardown-Vollständigkeit | Inventarvergleich vor/nach Deaktivierung für alle Contribution-Arten. |
| ALC-Entladbarkeit | `WeakReference`-Nachweis nach Quiescence und begrenzten Collection-Pässen; Pins werden als Versuchsergebnis klassifiziert. |
| Update-Stabilität | wiederholte Activate/Use/Deactivate/Update-Zyklen einschließlich absichtlich fehlerhafter Plugins. |
| Decorator-Reihenfolge | identischer Output bei gleicher Prioritäts-/Abhängigkeitskonfiguration über alle Wiederholungen. |
| Leistungsgrenze | p50/p95/p99 und Allokationen gegenüber einer statischen Baseline; akzeptable Obergrenze nach AP 0 festlegen. |
| Fehleratomarität | Fault Injection an jeder Registrierungs-/Teardown-Stufe; niemals fälschlich `Inactive` oder `Active`. |

### 5.2 Bereits vorhandene technische Evidenz

- [SharedContractAssemblyRegistry](../../src/Hosting/Application/Plugins/SharedContractAssemblyRegistry.cs)
  vereinheitlicht deklarierte Contracts im Default-Kontext und behandelt Major-Konflikte.
- [PluginAssemblyLoadContext](../../src/Hosting/Application/Plugins/PluginAssemblyLoadContext.cs)
  trennt private Abhängigkeiten in collectible Load Contexts.
- [RuntimePluginHost](../../src/Hosting/Application/Plugins/RuntimePluginHost.cs) prüft das
  tatsächliche Unloading über eine schwache Referenz und hält `UnloadFailed` sichtbar.
- [PluginAvailabilityEvaluator](../../src/Host/Backend/Application/Plugins/PluginAvailabilityEvaluator.cs)
  führt die sieben Verfügbarkeitsfaktoren zentral zusammen.
- Tests decken Availability, Shared Contracts, ALC-Unload und Decoration bereits punktuell ab.
- Der Git-Verlauf belegt Implementierungsschritte und Korrekturen besser als die ignorierten
  Architekturdokumente.

Diese Evidenz verbessert die Plausibilität des Vorhabens. Sie ist kein Ersatz für ein
FuE-Versuchsprotokoll und keinen Stundenbeleg.

## 6. Förderfähige und nicht förderfähige Tätigkeiten

### 6.1 Gute FuE-Kandidaten

- technische Hypothesen und alternative Runtime-Algorithmen entwickeln,
- Prototypen für Snapshot-, Proxy-, Drain- und Teardown-Verfahren bauen,
- Ursachen für ALC-Pins und inkonsistente Objektgraphen experimentell ermitteln,
- Konkurrenz-, Fault-Injection- und Wiederholungsversuche durchführen,
- Ergebnisse auswerten und den Lösungsweg aufgrund technischer Befunde ändern,
- einen fachfremden Referenzfall als technische Generalisierungsprobe bauen,
- negative Ergebnisse und nicht tragfähige Varianten dokumentieren.

### 6.2 Klar auszugrenzen

- reine Ordner-, Namespace-, Projekt- oder Repository-Umbauten,
- `dotnet new`-Template, NuGet-Veröffentlichung und SDK-Dokumentation,
- normale Admin-/Workspace-UI, Designsystem, CSS und Asset-Pipelines,
- Marketplace, Billing, Stripe, Publisher-Workflow und Marketing,
- gewöhnliche CRUD-Endpunkte, RBAC, Login, Standard-DSGVO-Funktionen und Runbooks,
- routinemäßige Security-Härtung, Signaturen, Secret-Management, SBOM und CVE-Gates,
- Standardintegration von PostgreSQL, Redis, OpenTelemetry oder ASP.NET Core,
- Produktpflege, Fehlerkorrekturen und Regressionstests ohne FuE-Bezug,
- Communication-, Dialer-, CMS- oder Intranet-Fachfunktionen, soweit sie nur die Plattform nutzen,
- Arbeiten nach technischer Festlegung, die ausschließlich Marktreife oder stabilen Betrieb
  herstellen.

Gemischte Commits oder Arbeitstage müssen zeitlich aufgeteilt werden. Ein Commit mit einem
FuE-relevanten Kern macht nicht automatisch die gesamte darin enthaltene Refactoring-, Test- oder
Dokumentationszeit förderfähig.

## 7. Zweite mögliche FuE-Linie

Die älteren Dokumente enthalten Voice-, Media-, Realtime-AI-, Risk-, Privacy- und Policy-Ideen.
Daraus ergibt sich derzeit **kein zweites ausreichend definiertes FuE-Vorhaben**. Solche Arbeiten
sollten nur separat beantragt werden, wenn eine eigene technische Wissenslücke, ein eigener
Versuchsplan und unabhängig prüfbare Ziele vorliegen. Communication dient im empfohlenen Vorhaben
lediglich als Foundation- und Referenzplugin; seine Fachfunktion darf nicht mit der allgemeinen
Runtime-Forschung vermischt werden.

## 8. Förderhöhe bei Eigenleistungen

Nach [§ 3 FZulG](https://www.gesetze-im-internet.de/fzulg/__3.html) werden für nachgewiesene
Eigenleistungen eines Einzelunternehmers ab 2026 **100 Euro je FuE-Arbeitsstunde**, höchstens
40 Stunden je Woche, als förderfähiger Aufwand angesetzt. Für nach dem 31.12.2025 begonnene
Vorhaben kommen pauschal 20 Prozent Gemein- und Betriebskosten hinzu. Die
[BSFZ-Übersicht zu den Änderungen ab 2026](https://www.bescheinigung-forschungszulage.de/steuerliches-investitionssofortprogramm)
bestätigt beide Werte.

Nach [§ 4 FZulG](https://www.gesetze-im-internet.de/fzulg/__4.html) beträgt die Zulage 25 Prozent.
Ein KMU im Sinne der AGVO kann zehn zusätzliche Prozentpunkte beantragen. Bei nachgewiesenem
KMU-Status und einem tatsächlich nach dem 31.12.2025 begonnenen Vorhaben ergibt sich daher:

```text
100 EUR Eigenleistung × 1,20 Gemeinkostenpauschale × 35 % = 42 EUR Zulage je anerkannter Stunde
```

| Anerkannte Inhaberstunden | Mit 20-%-Pauschale und 35-%-KMU-Satz | Ohne 20-%-Pauschale, aber mit 35-%-KMU-Satz |
|---:|---:|---:|
| 250 | 10.500 EUR | 8.750 EUR |
| 500 | 21.000 EUR | 17.500 EUR |
| 1.000 | 42.000 EUR | 35.000 EUR |

Ohne KMU-Erhöhung wären es rechnerisch 30 Euro beziehungsweise 25 Euro je anerkannter Stunde.
Der KMU-Status ist nicht allein wegen der Rechtsform sicher; Mitarbeiterzahl, Umsatz/Bilanzsumme
und gegebenenfalls Partner- oder verbundene Unternehmen sind zu prüfen.

Für den auf Eigenleistungen entfallenden Teil verweist § 9 Abs. 5 FZulG auf die
[De-minimis-Verordnung (EU) 2023/2831](https://eur-lex.europa.eu/eli/reg/2023/2831/oj).
Vor Antragstellung sind der verfügbare De-minimis-Rahmen und bereits erhaltene Beihilfen zu
prüfen.

Nur die tatsächlich persönlich geleistete und belegte Zeit des Inhabers zählt. Laufzeit eines
KI-Agenten, autonome Builds oder generierter Code sind keine Inhaberstunden. Förderfähig kann die
eigene Zeit für technische Konzeption, Versuchsanordnung, Implementierung, Review, Auswertung und
Fehleranalyse sein. Git- oder Agentenprotokolle können dies plausibilisieren, ersetzen aber den
Stundennachweis nicht.

## 9. Nachweis- und Ablagelogik

Ab sofort sollte jedes FuE-Arbeitsergebnis eine eindeutige Kennung wie `FUE-RUNTIME-01/AP-2`
tragen. Empfohlen wird folgende versionierte Struktur:

```text
ops/research/fue-runtime-01/
├── project-boundary.md
├── state-of-the-art.md
├── work-plan.md
├── risks-and-hypotheses.md
├── experiments/
│   ├── EXP-001-...
│   └── EXP-002-...
└── evidence-index.md
```

Außerhalb des Repositories beziehungsweise in der Buchhaltungsablage:

- täglicher Stundennachweis je FuE-Vorhaben und Arbeitspaket,
- kurze konkrete Tätigkeitsbeschreibung und technisches Ergebnis,
- Trennung zu Routine-, Produkt-, Support- und Verwaltungstätigkeiten,
- Lohn-/Auftragsunterlagen, Rechnungen und Zahlungsnachweise, falls einschlägig,
- De-minimis-Erklärungen und Nachweis des KMU-Status,
- BSFZ-Bescheid und späterer ELSTER-Antrag.

Das [BMF-Muster für FuE-Stundenzettel](https://www.bundesfinanzministerium.de/Content/DE/Standardartikel/Themen/Steuern/Steuerliche_Themengebiete/Forschungszulage/2021-11-11-FuE-Stundenzettel.pdf)
sollte als Mindestorientierung dienen. Rekonstruktionen für vergangene Tage dürfen nur auf
belastbaren zeitnahen Quellen beruhen und sollten konservativ sein.

Für jedes Experiment sollten festgehalten werden:

1. technische Frage und Hypothese,
2. Ausgangsstand und kontrollierte Randbedingungen,
3. Prototyp-/Variantenbeschreibung,
4. Messgrößen und vorab festgelegte Erfolgskriterien,
5. Rohdaten, Logs und reproduzierbarer Testbefehl,
6. Ergebnis einschließlich Fehlschlag,
7. Folgerung für die nächste Variante,
8. zugeordnete persönliche Arbeitszeit.

## 10. Arbeitsentwurf für die BSFZ-Fachfelder

Die Texte sind ein inhaltlicher Entwurf. Zeichenzahl, tatsächliche Projektzeiten, Messziele und
Stand-der-Technik-Recherche müssen vor Einreichung final geprüft werden.

### 10.1 Titel

> Workspace-spezifisch rekonfigurierbare Plugin-Control-Plane mit deterministischem Teardown für ASP.NET Core

### 10.2 Ziel und Wissenslücke

> Ziel ist die experimentelle Entwicklung eines Runtime-Verfahrens, mit dem Plugins in einer
> laufenden ASP.NET-Core-Plattform je Workspace aktiviert, deaktiviert und aktualisiert werden
> können, ohne den Prozess oder einen Tenant-Kernel neu zu starten. Neue Aufrufe sollen atomar die
> neue Konfiguration verwenden, während laufende Aufrufe kontrolliert beendet werden. Danach
> dürfen weder Service-Decorator, Routen, Event-Listener, Jobs, Contributor, Exports, Caches noch
> UI-Registrierungen die alte Plugininstanz oder deren AssemblyLoadContext referenzieren. Zugleich
> müssen öffentliche Vertragsassemblies eine gemeinsame Typidentität besitzen, private und
> versionskonfliktäre Abhängigkeiten aber isoliert bleiben. Ungeklärt ist, ob ein langlebiger
> Core-Proxy mit versionierten Registry-Snapshots und einer Drain-/Teardown-Phase diese Garantien
> unter parallelen Requests, unterschiedlichen Workspace-Zuständen und fehlerhaften Plugins ohne
> unvertretbaren Laufzeitaufwand erfüllen kann.

### 10.3 Abgrenzung zum Stand der Technik

> .NET AssemblyLoadContext isoliert Abhängigkeiten; McMaster.NETCore.Plugins vereinheitlicht Typen
> und lädt Plugins entladbar; OSGi spezifiziert dynamische Service-Registries und Bundle-Lifecycles;
> Orchard verwaltet Tenant-Features. Nicht belegt ist für ASP.NET Core die Gesamtgarantie aus
> workspace-spezifischem Wechsel von Services, Routen, Jobs und UI, konsistenter
> Shared-Type-Identity und anschließend entladbarem ALC unter Parallelzugriff. Diese
> Umsetzungslücke ist Gegenstand.

### 10.4 Arbeiten

> Zunächst werden reproduzierbare Baselines für stale Decorator Chains, ALC-Pinning,
> Vertragsversionskonflikte und parallele Workspace-Wechsel aufgebaut. Danach werden Varianten
> einer versionierten Decoration- und Contribution-Registry sowie eines stabilen Core-Proxys
> entwickelt. Für jede Variante werden Aktivierung, Quiescence, symmetrische Deregistrierung und
> Unloading implementiert. Eine Konfliktmatrix prüft Shared Type Identity gegenüber privaten
> Abhängigkeiten. Randomisierte Konkurrenztests verwenden gegensätzliche Pluginzustände in mehreren
> Workspaces. Fault Injection unterbricht jede Registrierungs- und Teardown-Stufe. Zwei fachlich
> verschiedene Referenzplugins prüfen die Domänenneutralität. Abschließend werden wiederholte
> Update-/Unload-Zyklen, Laufzeitkosten und verbliebene Referenzen gemessen und die Hypothese
> bestätigt, eingeschränkt oder verworfen.

### 10.5 Technische Risiken

> Bereits aufgelöste Singleton- oder Decorator-Graphen können deaktivierte Plugininstanzen halten
> und das ALC-Unloading dauerhaft verhindern. Aktivierungswechsel können mit Requests überlappen,
> sodass ohne geeignete Snapshot-Semantik gemischte Zustände entstehen. Prozessweit geladene
> Assemblies bei workspace-spezifischer Aktivierung können durch falsches Caching Beiträge in
> fremden Workspaces sichtbar machen. Shared Contracts können bei Versionskonflikten zu doppelter
> Typidentität oder Neustartzwang führen. Ein einzelner nicht deregistrierter Listener, Job, Cache
> oder laufender Request kann den gesamten Teardown ungültig machen. Schließlich kann die
> per-Aufruf-Auflösung zwar korrekt, aber zu langsam oder allokationsintensiv sein. Falls keine
> Variante diese Konflikte gemeinsam löst, ist ein Hot-Swap ohne Kernel-/Prozessneustart technisch
> nicht tragfähig.

## 11. Offene Tatsachen vor einem Antrag

1. Unter welcher Steuernummer und Einkunftsart wird das Einzelunternehmen geführt?
2. Trägt dieses Einzelunternehmen Kosten, Rechte und technisches Risiko des Callora-Vorhabens?
3. Was ist der sachlich richtige Projektbeginn, einschließlich möglicher Vorarbeiten in anderen
   Repositories oder für Dritte?
4. Ist die domänenneutrale Runtime ein abgrenzbares Folgevorhaben oder Fortsetzung der
   Voice-Plattform?
5. Ist der KMU-Status unter Einbeziehung verbundener beziehungsweise Partnerunternehmen erfüllt?
6. Welche De-minimis-Beihilfen wurden im relevanten Zeitraum bereits gewährt?
7. Welche bisherigen Inhaberstunden sind mit zeitnahen Unterlagen belastbar rekonstruierbar?
8. Gab oder gibt es andere Förderungen für dieselben Aufwendungen?
9. Welche Arbeiten wurden von Arbeitnehmern oder Auftragnehmern durchgeführt und in welchem
   EU-/EWR-Staat sitzen diese Auftragnehmer?
10. Welche quantitativen Ziele werden nach AP 0 verbindlich in den Arbeitsplan aufgenommen?

## 12. Empfohlene nächsten Schritte

1. Den Gegenstand auf `FUE-RUNTIME-01` festlegen und Routinearbeit explizit ausschließen.
2. Sachlichen Projektbeginn und Antragsteller anhand von Verträgen, Rechnungen, Repositories und
   ersten technischen Arbeiten schriftlich festhalten.
3. Ab sofort persönliche FuE-Zeit täglich und arbeitspaketbezogen erfassen.
4. AP 0 mit reproduzierbaren Baselines und eingefrorenen Messzielen durchführen.
5. Den Stand der Technik vor Antragstellung insbesondere gegen OSGi, McMaster.NETCore.Plugins,
   weitere dynamische Komponentenmodelle, konkrete wissenschaftliche Veröffentlichungen und
   gegebenenfalls Patente vertiefen; eine reine Produktvergleichsliste ist zu schwach.
6. Den BSFZ-Antrag für dieses eine Vorhaben stellen; er kann auch während der Durchführung gestellt
   werden. Danach wird die Zulage für das jeweilige Wirtschaftsjahr beim Finanzamt über ELSTER
   beantragt.
7. Steuerberater oder fachkundigen Berater nur die steuerlichen Tatsachen, KMU-/De-minimis-Prüfung
   und die finale Antragsschärfung validieren lassen; die technische Darstellung muss aus den
   tatsächlichen Versuchen stammen.

## 13. Externe Primärquellen

- [Forschungszulagengesetz, aktuelle Gesamtausgabe](https://www.gesetze-im-internet.de/fzulg/FZulG.pdf)
- [BSFZ: Hilfen zur Antragstellung und Prüfkriterien](https://www.bescheinigung-forschungszulage.de/hilfen-zur-antragstellung)
- [BSFZ: Änderungen ab 2026](https://www.bescheinigung-forschungszulage.de/steuerliches-investitionssofortprogramm)
- [BSFZ: Leitfaden zur Antragstellung 2026](https://www.bescheinigung-forschungszulage.de/dateien/PDF/20260210_Leitfaden_zur_Antragstellung.pdf)
- [BMF: Verfahren und steuerliche Forschungsförderung](https://www.bundesfinanzministerium.de/Web/DE/Themen/Steuern/Steuerliche_Themengebiete/Forschungszulage/forschungszulage.html)
- [BMF: Muster eines FuE-Stundenzettels](https://www.bundesfinanzministerium.de/Content/DE/Standardartikel/Themen/Steuern/Steuerliche_Themengebiete/Forschungszulage/2021-11-11-FuE-Stundenzettel.pdf)
- [Microsoft: .NET-Anwendung mit Plugin-Support](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)
- [Microsoft: Dependency Loading in .NET](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/overview)
- [Microsoft: AssemblyLoadContext und Typ-/Versionsidentität](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [Microsoft: Laden und Entladen von Assemblies](https://learn.microsoft.com/en-us/dotnet/standard/assembly/load-unload)
- [McMaster.NETCore.Plugins: Projekt und Primärdokumentation](https://github.com/natemcmaster/DotNetCorePlugins)
- [OSGi Core 8: Dynamic Service Layer](https://docs.osgi.org/specification/osgi.core/8.0.0/framework.service.html)
- [OSGi Core 7: Service- und Bundle-Tracker](https://docs.osgi.org/specification/osgi.core/7.0.0/util.tracker.html)
- [Orchard Core: Tenants](https://docs.orchardcore.net/en/latest/reference/modules/Tenants/)
- [Orchard Core: Features](https://docs.orchardcore.net/en/main/reference/modules/Features/)
