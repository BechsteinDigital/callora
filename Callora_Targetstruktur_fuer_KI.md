# Callora — Zielstruktur, Modularisierung und Produktgrenzen

Stand: 2026-04-14  
Zweck: Dieses Dokument beschreibt die empfohlene Zielstruktur von Callora als modularisierte Produktplattform. Es dient als Übergabegrundlage für KI-Systeme, Architekturplanung und spätere Umsetzungsarbeit.

---

## 1. Ausgangslage

Der aktuelle Solution-Zuschnitt ist noch relativ kompakt und besteht im Wesentlichen aus:

- `Callora.Core`
- `Callora.Audio.Windows`
- `Callora.Audio.Linux`
- `Callora.Tests`
- `Callora.Demo`
- `Callora.Performance`

Quelle: `Callora.sln` fileciteturn34file0

Das bedeutet:

- Der Großteil des aktuellen Produktwerts sitzt derzeit noch in `Callora.Core`.
- Audio ist bereits sinnvoll plattformspezifisch ausgelagert.
- Produktmodule wie `Callora.Intelligence`, `Callora.Privacy`, `Callora.Policy` und `Callora.Risk` existieren derzeit noch nicht als reale, getrennte Code-Module.
- Für ein belastbares Plugin- und Lizenzmodell muss die Struktur weiter modularisiert werden.

---

## 2. Strategische Zielsetzung

Callora soll langfristig **nicht nur eine weitere SIP-/VoIP-SDK** sein.

Die SIP-/VoIP-Basis ist die notwendige Grundlage, aber nicht die eigentliche Endpositionierung.
Die langfristige Differenzierung soll oberhalb des reinen Calling-Kerns entstehen.

### Langfristige Plattformlogik

- **Core** = Telephony Engine und stabile SDK-Fassade
- **Module / Plugins** = aufsetzende Produktfähigkeiten
- **Licensing** = kontrollierte Freischaltung je nach gekauftem Paket
- **Audio** = plattformspezifische Adapter
- **Zukunft** = Ausbau zur kontrollierbaren Voice-Plattform mit zusätzlichen Schichten wie Intelligence, Privacy, Policy und Risk

---

## 3. Grundprinzipien der Zielarchitektur

### 3.1 Core ist nicht „alles, was mit VoIP zu tun hat“

Der Core muss absichtlich klein und scharf geschnitten werden.

**Core ist alles, was zwingend nötig ist, damit ein Entwickler mit der SDK eine eigene Telefonie-Anwendung bauen kann.**

Nicht mehr. Aber auch nicht weniger.

### 3.2 Telephony Engine vs. Product Behavior

Saubere Trennregel:

- **Core = Telephony Engine**
- **Module = Product Behavior**

Das bedeutet:

- Signaling, Media, Call Lifecycle und Runtime-Grundlagen gehören in den Core.
- Dialer-Logik, Policy-Entscheidungen, Privacy-Funktionen, Intelligence und Risk gehören nicht in den Core.

### 3.3 Plugin-Denke ähnlich Shopware

Der Core stellt bereit:

- Runtime
- Domänenbasis
- Orchestrierung
- öffentliche SDK-Fassade
- Erweiterungspunkte

Module hängen sich darauf:

- registrieren Services
- erweitern Verhalten
- abonnieren Events
- bringen zusätzliche Produktdomänen mit
- sind separat lizenzierbar

### 3.4 Licensing ist nicht dasselbe wie Billing

- **Stripe** oder ein anderes Commerce-System ist für Kauf, Abo, Rechnung und Upgrades zuständig.
- **Licensing** ist die technische Entitlement-Schicht.
- **Core** kennt nur Feature Gates und Entitlement-Schnittstellen, nicht die komplette Commerce-Logik.

---

## 4. Definition des Core

## 4.1 Was der Core am Ende sein soll

Der Core ist die eigentliche VoIP-Engine, also ein sauber geschnittener technischer Unterbau für eigene Calling-Produkte.

Der Entwickler muss mit dem Core in der Lage sein:

- sich zu registrieren
- Anrufe aufzubauen
- Anrufe anzunehmen
- Anrufe zu beenden
- Hold / Unhold zu nutzen
- Transfers durchzuführen
- Audio anzubinden
- Medienpfad-Ereignisse und Call Events zu nutzen
- die SDK als Basis für eine eigene Anwendung einzusetzen

## 4.2 Was zwingend in den Core gehört

### Signaling / Session-Grundlagen
- SIP Signaling
- SDP
- Registration
- Dialog-/Session-Grundlagen
- Transactions
- Transport

### Media-Grundlagen
- RTP
- RTCP
- SRTP
- Media Session-Grundlagen
- Format Handling
- Basale Media Routing Primitives
- Jitter Buffer Grundlagen
- DTMF Grundlagen
- Runtime Metrics Hooks

### Call Lifecycle
- Dial
- Accept
- Hangup
- Hold / Unhold
- Blind Transfer
- Attended Transfer
- Call State Machine
- Domain Events rund um Calls

### SDK-Fassade
- `VoipClient`
- Einstiegspunkte
- Konfiguration
- Builder/Bootstrap
- öffentliche Events

### Erweiterungspunkte
- Audio-Abstraktionen
- Modul-Abstraktionen
- Licensing-Abstraktionen
- DI-/Hosting-Hooks

---

## 5. Was nicht in den Core gehört

Folgende Dinge gehören **nicht** in den Core, sofern Monetarisierung und Modularisierung ernst gemeint sind:

- Privacy
- Risk
- Policy
- Intelligence
- Recording
- Playback
- Dialer-Logik
- Contact-Center-Logik
- CRM-nahe Produktlogik
- Kampagnen- oder Routing-Domänen
- Reporting / Analytics
- Tenant-Regeln auf Produktebene
- komplexe Governance- oder Compliance-Schichten

### Sonderfall Conference

Conference ist architektonisch noch diskutierbar.

Es gibt zwei mögliche Wege:

#### Variante A — Conference bleibt im Core
Vorteile:
- sehr starkes Basispaket
- direkter Mehrwert für Entwickler

Nachteile:
- schwächere Monetarisierungsgrenze
- Core wird größer

#### Variante B — Conference wird Modul
Vorteile:
- sauberer Upsell
- klarere Produktgrenze
- Core bleibt fokussierter

Nachteile:
- Basispaket wirkt schmaler

Empfehlung: Conference eher als eigenes Modul auslagern, wenn die modulare Monetarisierung ernst gemeint ist.

---

## 6. Zielstruktur als Projektbaum

```text
Callora.sln
│
├── src
│   ├── Callora.Core
│   │   ├── Domain
│   │   │   ├── Calls
│   │   │   ├── Lines
│   │   │   ├── Transfers
│   │   │   ├── Media
│   │   │   ├── Signaling
│   │   │   ├── Events
│   │   │   ├── Policies
│   │   │   └── ValueObjects
│   │   │
│   │   ├── Application
│   │   │   ├── Calls
│   │   │   ├── Lines
│   │   │   ├── Transfers
│   │   │   ├── Media
│   │   │   ├── Runtime
│   │   │   ├── Ports
│   │   │   └── Services
│   │   │
│   │   ├── Infrastructure
│   │   │   ├── Common
│   │   │   ├── Sip
│   │   │   │   ├── Parsing
│   │   │   │   ├── Transactions
│   │   │   │   ├── Transport
│   │   │   │   ├── Registration
│   │   │   │   ├── Dialogs
│   │   │   │   └── Messages
│   │   │   ├── Rtp
│   │   │   │   ├── Transport
│   │   │   │   ├── Jitter
│   │   │   │   ├── Dtmf
│   │   │   │   └── Metrics
│   │   │   ├── Rtcp
│   │   │   ├── Srtp
│   │   │   ├── Sdp
│   │   │   ├── Media
│   │   │   │   ├── Pipelines
│   │   │   │   ├── Mixers
│   │   │   │   ├── Routing
│   │   │   │   └── Formats
│   │   │   └── DependencyInjection
│   │   │
│   │   ├── Sdk
│   │   │   ├── VoipClient
│   │   │   ├── Builders
│   │   │   ├── Configuration
│   │   │   ├── Events
│   │   │   ├── Extensions
│   │   │   └── FeatureGates
│   │   │
│   │   └── Abstractions
│   │       ├── Audio
│   │       ├── Licensing
│   │       ├── Modules
│   │       └── Hosting
│   │
│   ├── Callora.Audio.Abstractions
│   │   ├── Devices
│   │   ├── Streams
│   │   ├── Formats
│   │   └── Calibration
│   │
│   ├── Callora.Audio.Windows
│   ├── Callora.Audio.Linux
│   ├── Callora.Audio.Headless
│   │
│   ├── Callora.Licensing
│   │   ├── Domain
│   │   ├── Application
│   │   ├── Infrastructure
│   │   ├── Tokens
│   │   ├── Entitlements
│   │   ├── Validation
│   │   ├── Offline
│   │   └── DependencyInjection
│   │
│   ├── Callora.Modules.Abstractions
│   │   ├── Contracts
│   │   ├── Manifest
│   │   ├── Registration
│   │   ├── Lifecycle
│   │   └── FeatureFlags
│   │
│   ├── Callora.Conferencing
│   ├── Callora.Recording
│   ├── Callora.Playback
│   ├── Callora.Dialer
│   ├── Callora.ContactCenter
│   │
│   ├── Callora.Privacy
│   ├── Callora.Risk
│   ├── Callora.Policy
│   ├── Callora.Intelligence
│   │
│   ├── Callora.Hosting
│   │   ├── MicrosoftDependencyInjection
│   │   ├── Options
│   │   ├── Startup
│   │   └── ModuleBootstrap
│   │
│   └── Callora.SharedKernel        # nur wenn wirklich nötig, sonst vermeiden
│
├── samples
│   ├── Callora.Sample.BasicCalling
│   ├── Callora.Sample.Transfer
│   ├── Callora.Sample.Conference
│   ├── Callora.Sample.CustomAudio
│   ├── Callora.Sample.Dialer
│   └── Callora.Sample.ModuleHost
│
├── tests
│   ├── Callora.Core.Tests
│   ├── Callora.Core.IntegrationTests
│   ├── Callora.Audio.Tests
│   ├── Callora.Licensing.Tests
│   ├── Callora.Modules.Tests
│   ├── Callora.Conferencing.Tests
│   ├── Callora.Privacy.Tests
│   ├── Callora.Risk.Tests
│   ├── Callora.Policy.Tests
│   ├── Callora.Intelligence.Tests
│   └── Callora.SoakTests
│
├── perf
│   ├── Callora.Core.Performance
│   ├── Callora.Media.Performance
│   └── Callora.Conferencing.Performance
│
└── docs
    ├── architecture
    ├── modules
    ├── licensing
    ├── public-api
    ├── product
    └── roadmap
```

---

## 7. Audio-Schnitt

Audio ist bereits heute der sauberste Hinweis auf die gewünschte Modularisierung.

Daraus folgt als Zielmodell:

- `Callora.Audio.Abstractions` = allgemeine Audio-Verträge
- `Callora.Audio.Windows` = Windows-spezifische Implementierung
- `Callora.Audio.Linux` = Linux-spezifische Implementierung
- `Callora.Audio.Headless` = Headless/Test/Server-Fälle

Wichtig:

- Der Core darf nie hart an eine konkrete Plattform-Audio-Implementierung gebunden sein.
- Alle Audio-Module sollen austauschbar sein.

---

## 8. Licensing-Schnitt

## 8.1 Grundregel

`Callora.Licensing` ist ein eigenes Modul.

Der Core kennt nur abstrakte Feature Gates und Entitlement-Schnittstellen.

## 8.2 Was in den Core darf

Nur Verträge wie zum Beispiel:

- `ILicenseFeatureGate`
- `ILicenseSnapshotProvider`
- `IFeatureEntitlement`
- `IModuleAvailability`

## 8.3 Was in `Callora.Licensing` gehört

- Tokenvalidierung
- Signaturen
- Lizenzprüfung
- Modulfreischaltung
- Limits
- Offline-Lizenzen
- Lizenz-Refresh
- Entitlement-Mapping
- eventuelle Stripe-seitige Zuordnung im Backend-Kontext

Wichtig:

- Stripe ist nicht die Lizenzlogik.
- Stripe ist Commerce.
- Licensing ist Entitlement- und Produktfreischaltungslogik.

---

## 9. Modulmodell / Pluginmodell

Damit die Struktur später tatsächlich „pluginartig“ wird, braucht Callora ein explizites Modulmodell.

## 9.1 Modul-Abstraktionen

In `Callora.Modules.Abstractions` sollten mindestens definiert werden:

- `ICalloraModule`
- `ICalloraModuleManifest`
- `ICalloraModuleBootstrapper`
- `ICalloraModuleServiceRegistration`
- `ICalloraFeatureDescriptor`

## 9.2 Fähigkeiten eines Moduls

Ein Modul darf:

- Services registrieren
- Event Handler anmelden
- neue Features beschreiben
- bestehende Extension Points nutzen
- Feature Gates deklarieren
- Runtime Hooks nutzen
- optional eigene Konfiguration mitbringen

## 9.3 Was Module nicht tun sollten

Module sollen nicht:

- willkürlich Core-Internals patchen
- harte Abhängigkeiten in beide Richtungen erzeugen
- die Public API des Core chaotisch aufbrechen

---

## 10. Produktlogik und Modulschnitt

## 10.1 Basispaket

Das kommerzielle Basispaket sollte mindestens enthalten:

- `Callora.Core`
- `Callora.Audio.Abstractions`
- mindestens ein Audio-Provider
- optional `Callora.Hosting`
- `Callora.Licensing` für die kontrollierte Freischaltung

## 10.2 Erweiterungsmodule

Mögliche mittlere Modulpakete:

- `Callora.Conferencing`
- `Callora.Recording`
- `Callora.Playback`
- `Callora.Dialer`
- `Callora.ContactCenter`

## 10.3 Strategische Premium-Module

Die eigentliche spätere Differenzierung:

- `Callora.Privacy`
- `Callora.Risk`
- `Callora.Policy`
- `Callora.Intelligence`

Diese vier Module sind die eigentliche Zukunftsschicht oberhalb des reinen Calling-Kerns.

---

## 11. Open-Source-Strategie

## 11.1 Aktuelle Einschätzung

Solange die strategischen Premium-Module noch nicht real existieren, sollte Callora nicht öffentlich gemacht werden.

Begründung:

- Der aktuelle Produktwert sitzt heute noch primär im Core.
- Ohne reale Aufsatzmodule wäre ein öffentlicher Core faktisch fast schon das komplette Produkt.
- Dann würde Callora zunächst als „noch eine SIP-/VoIP-SDK“ wahrgenommen werden, obwohl die eigentliche Vision darüber hinausgeht.

## 11.2 Zielperspektive

Sobald folgende Module real stehen oder mindestens glaubwürdig begonnen wurden, kann die Entscheidung neu bewertet werden:

- `Callora.Intelligence`
- `Callora.Privacy`
- `Callora.Policy`
- `Callora.Risk`

Dann wäre der Core grundsätzlich so geschnitten, dass er auch offenlegbar sein könnte, ohne den gesamten Zukunftswert zu verschenken.

## 11.3 Empfohlene Reihenfolge

1. Modulgrenzen technisch sauber ziehen
2. Premium-/Differenzierungsmodule real bauen
3. Licensing und Entitlements etablieren
4. Erst dann Open-Core oder öffentliche Repo-Strategie neu bewerten

---

## 12. Dependency-Regeln

Empfohlene harte Regeln:

### 12.1 Core-Regeln
- `Callora.Core` referenziert keine Produktmodule.
- `Callora.Core` kennt nur Abstraktionen, keine konkreten Commercial-Implementierungen.
- `Callora.Core` darf nicht direkt von `Callora.Licensing` abhängen, sondern nur von dessen Verträgen.

### 12.2 Modulregeln
- Module dürfen `Callora.Core` referenzieren.
- Module dürfen `Callora.Modules.Abstractions` referenzieren.
- Module dürfen optional `Callora.Licensing` referenzieren, wenn für Feature Gates nötig.
- Module dürfen nicht untereinander implizit zyklisch werden.

### 12.3 Audio-Regeln
- Audio-Provider referenzieren Audio-Abstraktionen und Core.
- Core referenziert nie einen konkreten Audio-Provider.

### 12.4 Test-Regeln
- Unit-Tests pro Projekt
- Integrationstests für Kernabläufe
- Soak-Tests separat
- Performance-Projekte strikt von normalen Tests getrennt

---

## 13. Migrationsvorschlag in Stufen

## Stufe 1 — Repo sauber schneiden

- `src/`, `tests/`, `samples/`, `perf/`, `docs/` einführen
- bestehende Projekte strukturell dorthin überführen
- Demo, Tests und Performance logisch trennen

## Stufe 2 — Audio abstrahieren

- `Callora.Audio.Abstractions` einführen
- Windows und Linux darüber sauber anschließen
- optional Headless-Audio ergänzen

## Stufe 3 — Licensing abstrahieren

- Feature Gate Interfaces definieren
- `Callora.Licensing` als eigenes Projekt einführen
- Core entkoppeln

## Stufe 4 — Modul-Framework einführen

- `Callora.Modules.Abstractions` bauen
- Modul-Manifest, Bootstrapper, Feature-Deskriptoren definieren
- Modulregistrierung über DI/Hosting ermöglichen

## Stufe 5 — Erste echte Modulabspaltung

Empfohlene erste Kandidaten:

- `Callora.Conferencing`
- `Callora.Recording`
- `Callora.Playback`

## Stufe 6 — Strategische Module starten

- `Callora.Privacy`
- `Callora.Risk`
- `Callora.Policy`
- `Callora.Intelligence`

## Stufe 7 — Open-Core-Entscheidung neu bewerten

Erst wenn der Core nicht mehr fast das gesamte Produkt allein trägt.

---

## 14. Kurzfazit

Die Zielstruktur von Callora sollte so aufgebaut werden:

- **Core** als klarer, technisch belastbarer Calling-/VoIP-Kern
- **Audio** als saubere, plattformspezifische Adapterwelt
- **Licensing** als separate Entitlement-Schicht
- **Module** als pluginartige Aufsatzfähigkeiten
- **Premium-Schicht** als eigentliche strategische Differenzierung

Die wichtigste Leitformel dafür lautet:

> **Core = Telephony Engine + stabile SDK-Fassade + Erweiterungspunkte**  
> **Module = alles, was darüber zusätzliche Produktfähigkeit schafft**

Damit bleibt Callora:

- technisch sauber
- produktstrategisch erweiterbar
- kommerziell lizenzierbar
- später grundsätzlich open-core-fähig
- deutlich besser positionierbar als reine SIP-/VoIP-SDK

