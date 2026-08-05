# Communication-Foundation — Implementierungsplan

> **Für ausführende Worker:** Jeder Baustein läuft nach der Repo-Kette
> role-dev → role-reviewer → alle Findings fixen → grün → ff-merge → Branch löschen.
> Commit-Trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
> **Push nur auf ausdrückliche Anweisung.** Schritte nutzen Checkbox-Syntax (`- [ ]`).

**Goal:** Clean-Slate-Neuaufbau des System-Tier-Communication-Plugins + neu entworfene
`Callora.Plugin.Communication.Abstractions` als Foundation für Dritt-/Eigen-Plugins
(Erstkunde AiAgent).

**Architecture:** Channel-agnostische Abstractions (Voice-Audio-Erweiterung als
AiAgent-Andockpunkt) + voll ausgebauter Voice-Channel über CalloraVoipSdk 4.6, komplett in
`Infrastructure/Sdk` gekapselt. Domänenmodell `SipAccount → SipLine → Call/CallLog`.
Schichten Domain → Application → Infrastructure → Api, Verdrahtung nur im Composition Root.

**Tech Stack:** .NET 10, EF Core 10 (Schema `plugin_communication`), CalloraVoipSdk
4.6.0-preview.1 (lokaler Feed), xUnit, PublicApiAnalyzers, Governance-Analyzer CAL0001–0004.

**Grundlage:** `ops/specs/2026-07-21-communication-foundation-clean-slate-design.md`.

---

## Plan-Umfang & Baustein-Reihenfolge

Reihenfolge **gegenüber der Spec reordert**: Archivieren zuerst (B0), weil das neue
Abstractions-Projekt denselben Assembly-Namen/Pfad wie das alte belegt — Koexistenz
unmöglich.

| Baustein | Inhalt | Dieser Plan |
|---|---|---|
| **B0** | Grund freiräumen: Alt-Plugin + Alt-Abstractions + Dialer archivieren, Tests/Refs bereinigen | **voll bite-sized (unten)** |
| **B1** | Neue Abstractions (Verträge + Contract-Tests) | **voll bite-sized (unten)** |
| B2 | Domain + Persistenz (`SipAccount/SipLine/CallLog`, DbContext, Migration, Stores, Purge) | Folge-Plan |
| B3 | SDK-Bridge + VoiceChannel (Registration-Lifecycle, `IVoipCall`/`ICallAudioStream`) | Folge-Plan *(braucht SDK-4.6-API-Discovery)* |
| B4 | Inbound/Outbound + Flows + Consent + CallLog-Finalisierung | Folge-Plan |
| B5 | Admin-API (+ optional Surface) | Folge-Plan |

Jeder Folge-Plan wird geschrieben, wenn sein Vorgänger gemerged ist (ein Plan pro
Subsystem). B3 erhält seinen Detailplan erst nach B2, weil die konkrete 4.6-API
(die entfallenen `Core.Security`/`SdkConfiguration` haben Nachfolger) beim Bauen der Bridge
verifiziert werden muss — vorher wäre der Code erfunden.

---

## File Structure (Zielzustand nach B1)

```
custom/static-plugins/
├── _archive/
│   └── Communication-legacy/        # B0: altes Plugin + Abstractions (git mv, History bleibt)
│   └── Dialer-legacy/               # B0: Dialer (referenzierte alte Abstractions)
└── Communication/
    └── Abstractions/
        ├── Callora.Plugin.Communication.Abstractions.csproj
        └── src/
            ├── Calls/               # ICall, CallState, CallDirection, CallTarget, Events (preserved)
            ├── Channels/            # ICommunicationChannel(+Health), ICommunicationChannelRegistry
            ├── Capabilities/        # CommunicationCapabilities
            ├── Consent/             # IRecordingConsentCall + Consent-Typen (preserved)
            ├── Voice/               # NEU: IVoiceChannel, IVoipCall, ICallAudioStream, AudioFormat, AudioFrameReceivedEventArgs
            └── Status/              # NEU: SipAccountStatus, SipLineStatus (read-only Enums für Consumer)
```

---

## Baustein B0 — Grund freiräumen (Archivierung)

**Files:**
- Move: `custom/static-plugins/Communication/` → `custom/static-plugins/_archive/Communication-legacy/`
- Move: `custom/plugins/Dialer/` → `custom/static-plugins/_archive/Dialer-legacy/`
- Modify: `Callora.Host.sln` (Projekte entfernen)
- Modify: `Directory.Packages.props` (CalloraVoipSdk-Eintrag bleibt; VersionOverride entfällt mit dem Alt-csproj)
- Remove: `tests/Callora.Core.Tests/…` Tests, die die alte Communication-Impl referenzieren

- [ ] **Schritt 1: Vollständige Referenzfläche ermitteln**

Run:
```bash
cd /home/dbechstein/Projekte/callora
grep -rln "Callora.Plugin.Communication\|Callora.Plugins.Dialer\|Communication.Abstractions" \
  --include=*.cs --include=*.csproj --include=*.sln . | grep -v -E '/(bin|obj|node_modules)/'
```
Erwartung: das alte Communication-Plugin + Abstractions, Dialer, `Callora.Host.sln`, sowie
Testdateien in `tests/Callora.Core.Tests` (mind. `CommunicationChannelRegistryTests`, ggf.
Voip-Consent-Tests). Die Trefferliste ist die Removal-Checkliste für die nächsten Schritte.

- [ ] **Schritt 2: Branch anlegen**

Run: `git checkout -b chore/communication-b0-archive`

- [ ] **Schritt 3: Alt-Plugin + Alt-Abstractions archivieren**

Run:
```bash
mkdir -p custom/static-plugins/_archive
git mv custom/static-plugins/Communication custom/static-plugins/_archive/Communication-legacy
```

- [ ] **Schritt 4: Dialer archivieren** (referenziert die alte Abstractions; Re-Homing auf die neuen Verträge ist Follow-up)

Run:
```bash
git mv custom/plugins/Dialer custom/static-plugins/_archive/Dialer-legacy
```

- [ ] **Schritt 5: Projekte aus der Solution entfernen**

Run:
```bash
dotnet sln Callora.Host.sln remove \
  custom/static-plugins/_archive/Communication-legacy/Callora.Plugin.Communication.csproj \
  custom/static-plugins/_archive/Communication-legacy/Abstractions/Callora.Plugin.Communication.Abstractions.csproj \
  custom/static-plugins/_archive/Dialer-legacy/Callora.Plugins.Dialer.csproj
```
Falls `dotnet sln remove` die Pfade nach dem `git mv` nicht findet, die drei
`Project(...)`-Blöcke direkt aus `Callora.Host.sln` entfernen (verifizieren mit
`grep -n "Communication\|Dialer" Callora.Host.sln`).

- [ ] **Schritt 6: Test-Referenzen auf die Alt-Impl entfernen**

Für jede in Schritt 1 gefundene Testdatei unter `tests/`, die
`Callora.Plugin.Communication.Application.*`/`.Channels`/Dialer-Typen referenziert
(mind. `CommunicationChannelRegistryTests`): `git rm` die Datei — sie prüft archivierten
Code und wird beim Neubau durch Contract-Tests (B1) und Integrationstests (B2–B4) ersetzt.

Run (Beispiel, exakte Liste aus Schritt 1):
```bash
git rm tests/Callora.Core.Tests/Application/Communication/CommunicationChannelRegistryTests.cs
```

- [ ] **Schritt 7: Restore + Build + volle Suite**

Run:
```bash
dotnet build Callora.Host.sln --nologo -v minimal -nodeReuse:false -p:UseSharedCompilation=false
dotnet test  Callora.Host.sln --nologo -v minimal
```
Erwartung: **Build 0/0**, Suite grün (Testzahl sinkt um die in Schritt 6 entfernten Tests).
Es gibt jetzt **kein** Communication-Plugin mehr — bewusst (Clean-Slate-Zwischenzustand;
AiAgent existiert noch nicht, Dialer ist archiviert).

- [ ] **Schritt 8: Commit + Merge**

```bash
git add -A
git commit -m "chore(communication): Alt-Plugin + Abstractions + Dialer archivieren (B0)

Clean-Slate-Vorbereitung: das alte System-Tier-Communication-Plugin inkl.
Abstractions und das Dialer-Referenzplugin (haengt an der alten Abstractions)
nach custom/static-plugins/_archive/ verschoben und aus der Solution entfernt;
Alt-Impl-Tests entfernt. Build 0/0.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>"
git checkout main && git merge --ff-only chore/communication-b0-archive && git branch -d chore/communication-b0-archive
```

**Akzeptanz B0:** kein Communication/Dialer-Projekt mehr in der Solution; Alt-Code unter
`_archive/` (History erhalten); Build 0/0; Suite grün. Follow-up notiert: Dialer auf neue
Abstractions re-homen oder endgültig verwerfen.

---

## Baustein B1 — Neue Abstractions

**Files:**
- Create: `custom/static-plugins/Communication/Abstractions/Callora.Plugin.Communication.Abstractions.csproj`
- Create: `custom/static-plugins/Communication/Abstractions/PublicAPI.Shipped.txt` + `PublicAPI.Unshipped.txt`
- Create: `.../Abstractions/src/**` (Verträge, §5 der Spec)
- Create: `tests/Callora.Core.Tests/Communication/Abstractions/*` (Contract-Tests)
- Modify: `Callora.Host.sln` (neues Abstractions-Projekt aufnehmen)

- [ ] **Schritt 1: Branch**

Run: `git checkout -b feat/communication-b1-abstractions`

- [ ] **Schritt 2: csproj anlegen** (net10.0, Governance-Analyzer, PublicApiAnalyzers — wie das alte Abstractions-csproj, aus `_archive` als Vorlage)

`custom/static-plugins/Communication/Abstractions/Callora.Plugin.Communication.Abstractions.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="all">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <AdditionalFiles Include="PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
  </ItemGroup>
</Project>
```
Beide `PublicAPI.*.txt` leer anlegen (Unshipped wird über den Build gefüllt, RS0016-getrieben).

- [ ] **Schritt 3: Preserved Contracts aus dem Archiv übernehmen** (unverändert, nur Ablage)

Kopiere aus `custom/static-plugins/_archive/Communication-legacy/Abstractions/src/` in das
neue `.../Abstractions/src/` **unverändert** (Namespace `Callora.Plugin.Communication.Abstractions`
bleibt gleich):
`Calls/ICall.cs`, `Calls/CallState.cs`, `Calls/CallDirection.cs`, `Calls/CallTarget.cs`,
`Calls/CallStateChangedEventArgs.cs`, `Calls/IncomingCallEventArgs.cs`, `Calls/CallSummary.cs`,
`Calls/CallEventTypes.cs`, `Channels/ICommunicationChannel.cs`,
`Channels/ICommunicationChannelRegistry.cs`, `Capabilities/CommunicationCapabilities.cs`,
`Consent/IRecordingConsentCall.cs`, `Consent/RecordingConsentChangedEventArgs.cs`,
`Consent/RecordingConsentRequest.cs`, `Consent/RecordingConsentResult.cs`,
`Consent/RecordingConsentState.cs`.
**Nicht** übernehmen (verworfen, §5): `Calls/ICallDirectory.cs`, `Calls/ICallEventStream.cs`,
`Calls/ICallEventSubscription.cs`, `Calls/CallStreamEvent.cs`.

- [ ] **Schritt 4: `ChannelHealth` + Channel-Erweiterung** (Contract-Test zuerst)

Test `tests/Callora.Core.Tests/Communication/Abstractions/ChannelHealthContractTests.cs`:
prüft, dass `ChannelHealth` die Werte `Unknown/Up/Degraded/Down` hat und
`ICommunicationChannel.Health` gelesen werden kann (Test-Double-Channel). Test rot laufen
lassen (`Health` existiert noch nicht).

Dann `src/Channels/ChannelHealth.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Aggregierter Gesundheitszustand eines Channels, abgeleitet aus dem
/// Verbindungsstatus seiner Accounts — read-only für Konsumenten.</summary>
public enum ChannelHealth { Unknown = 0, Up = 1, Degraded = 2, Down = 3 }
```
Und in `src/Channels/ICommunicationChannel.cs` die Property ergänzen:
```csharp
    /// <summary>Aktueller Gesundheitszustand des Channels.</summary>
    ChannelHealth Health { get; }
```
Test grün. PublicAPI.Unshipped.txt via Build füllen (RS0016 zeigt die Zeilen).

- [ ] **Schritt 5: Status-Enums (read-only für Consumer)**

`src/Status/SipAccountStatus.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Konnektivitätsstatus eines SIP-Accounts (Registration/Trunk-Erreichbarkeit).</summary>
public enum SipAccountStatus { Disabled = 0, Connecting = 1, Up = 2, Degraded = 3, Failed = 4 }
```
`src/Status/SipLineStatus.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Betriebsverfügbarkeit einer SIP-Line (abgeleitet aus Account-Status,
/// Enabled und aktueller Call-Belegung).</summary>
public enum SipLineStatus { Disabled = 0, Unavailable = 1, Available = 2, Busy = 3 }
```

- [ ] **Schritt 6: Audio-Format-Wert** (Contract-Test zuerst)

Test: `AudioFormat` trägt Codec/SampleRate/FrameMs, Default `G711`/`8000`/`20`. Rot → grün.

`src/Voice/AudioCodec.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Audio-Codec eines Call-Streams. v1: G.711 (µ-law/A-law) für SIP/PSTN.</summary>
public enum AudioCodec { G711Ulaw = 0, G711Alaw = 1 }
```
`src/Voice/AudioFormat.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Beschreibt das Frame-Format eines <see cref="ICallAudioStream"/>.</summary>
public sealed record AudioFormat(AudioCodec Codec, int SampleRateHz, int FrameMilliseconds)
{
    /// <summary>SIP/PSTN-Standard: G.711 µ-law, 8 kHz, 20-ms-Frames.</summary>
    public static AudioFormat G711Ulaw8k20ms { get; } = new(AudioCodec.G711Ulaw, 8000, 20);
}
```

- [ ] **Schritt 7: Duplex-Audio-Port** (Contract-Test zuerst)

Test `AudioDuplexContractTests`: ein Test-Double-`ICallAudioStream` feuert `FrameReceived`
mit einem Frame, ein Consumer liest ihn; `SendAsync` nimmt einen Frame entgegen. Prüft den
**Nicht-Blockieren**-Vertrag nur dokumentarisch (Handler kehrt sofort zurück). Rot → grün.

`src/Voice/AudioFrameReceivedEventArgs.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Ein eingehender Audio-Frame (inbound). Handler dürfen NICHT blockieren —
/// Frame in eine Queue schreiben und sofort zurückkehren.</summary>
public sealed class AudioFrameReceivedEventArgs(ReadOnlyMemory<byte> frame) : EventArgs
{
    /// <summary>Der rohe kodierte Frame gemäß <see cref="ICallAudioStream.Format"/>.</summary>
    public ReadOnlyMemory<byte> Frame { get; } = frame;
}
```
`src/Voice/ICallAudioStream.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Bidirektionaler Audio-Stream eines Voice-Calls. Inbound über
/// <see cref="FrameReceived"/>, outbound über <see cref="SendAsync"/> — der präzise
/// Sende-Takt (monotone Clock, kein Task.Delay) liegt beim Consumer.</summary>
public interface ICallAudioStream : IAsyncDisposable
{
    /// <summary>Format der Frames in beiden Richtungen.</summary>
    AudioFormat Format { get; }

    /// <summary>Eingehende Frames. Handler dürfen nicht blockieren.</summary>
    event EventHandler<AudioFrameReceivedEventArgs>? FrameReceived;

    /// <summary>Sendet einen ausgehenden Frame an die Gegenstelle.</summary>
    ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);
}
```

- [ ] **Schritt 8: Voice-Call- und -Channel-Erweiterung**

`src/Voice/IVoipCall.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Voice-spezifischer Call: der modalitätsneutrale <see cref="ICall"/> plus
/// Zugriff auf den Duplex-Audio-Stream (ADR-012/REV2 §10.1 C).</summary>
public interface IVoipCall : ICall
{
    /// <summary>Öffnet den bidirektionalen Audio-Stream des Calls.</summary>
    Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default);
}
```
`src/Voice/IVoiceChannel.cs`:
```csharp
namespace Callora.Plugin.Communication.Abstractions;

/// <summary>Ein Channel mit Voice-Fähigkeit: Calls sind <see cref="IVoipCall"/> mit
/// Audio-Zugriff.</summary>
public interface IVoiceChannel : ICommunicationChannel
{
}
```

- [ ] **Schritt 9: Solution + Build + Contract-Tests**

```bash
dotnet sln Callora.Host.sln add custom/static-plugins/Communication/Abstractions/Callora.Plugin.Communication.Abstractions.csproj
dotnet build Callora.Host.sln --nologo -v minimal -nodeReuse:false -p:UseSharedCompilation=false
dotnet test Callora.Host.sln --nologo -v minimal
```
Erwartung: Build 0/0 (PublicAPI.Unshipped.txt vollständig, sonst RS0016 → Zeilen ergänzen),
Contract-Tests grün.

- [ ] **Schritt 10: role-reviewer → Findings fixen → Commit + Merge** (kein Push)

**Akzeptanz B1:** neues `Callora.Plugin.Communication.Abstractions` kompiliert; channel-
agnostischer Kern erhalten, Voice-Audio-Erweiterung + Status-Enums neu, verworfene
Stream-Verträge entfernt; Contract-Tests grün; PublicAPI-Baseline vollständig; Build 0/0.

---

## Folge-Pläne (Roadmap, je eigener Detailplan)

**B2 — Domain + Persistenz.** `src/Domain/{Accounts,Lines,Calls}` (SipAccount, SipLine,
CallLog + Enums/VOs, framework-frei), `src/Infrastructure/Persistence/CommunicationDbContext`
(Schema `plugin_communication`) + Configurations + Migration, `ISipAccountStore`/
`ISipLineStore`/`ICallLogStore` (Ports in Application, EF-Adapter in Infrastructure),
`CommunicationDataPurgeContributor` + Retention. **Line-Erstellung über einen gate-baren
Service** (Cloud-Limit-Haken, §12). Akzeptanz: DbContext migriert, Stores + Purge getestet.

**B3 — SDK-Bridge + VoiceChannel** *(Voraussetzung: SDK-4.6-API verifizieren — Nachfolger
von `Core.Security`/`SdkConfiguration` ermitteln)*. `src/Infrastructure/Sdk`
(Registration-Engine, `VoiceChannel : IVoiceChannel`, `IVoipCall`/`ICallAudioStream`-Impl,
`src/Infrastructure/Audio` G711/PCM-Bridge), Registry-Registrierung, `account.*`/`line.*`-
Business-Events. Akzeptanz: Loopback-Contract-Test des Audio-Pfades grün.

**B4 — Inbound/Outbound + Flows + Consent.** `InboundRouter`, `CallCoordinator`,
`PlaceCallAsync`, Flow-Actions (call.accept/audio.play), Consent-Kopplung,
CallLog-Anlage/Finalisierung, `call.*`-Events. Akzeptanz: Inbound→Accept→Audio und
Outbound→Audio als Integrationstest.

**B5 — Admin-API (+ optional Surface).** `CommunicationAdminApiExtensionContributor`
(Account/Line CRUD + Live-Status, Call-History-Read, Re-Register), Permission-Keys, RBAC.
Optionales Vue-Admin-Modul. Akzeptanz: CRUD/Status/History über die Admin-API getestet.

**Danach:** Dialer auf neue Abstractions re-homen oder verwerfen; AiAgent-Plugin (eigener
Spec/Plan-Zyklus) auf dieser Foundation.

---

## Self-Review (writing-plans)

- **Spec-Abdeckung:** B0 deckt §3 (Archivierung); B1 deckt §5 (Abstractions-Neuentwurf,
  keep/add/drop); B2–B5 decken §4/§6–§9 (Domain, SDK-Bridge, Flows, Admin) als Folge-Pläne.
- **Platzhalter:** B0/B1 ohne Platzhalter (exakte Kommandos + vollständiger Vertrags-Code).
  B2–B5 sind bewusst Roadmap, kein Task-mit-Platzhalter — B3-Code wäre ohne SDK-4.6-API
  erfunden (ehrliche Grenze, kein Placeholder).
- **Typ-Konsistenz:** Namen (`IVoipCall.OpenAudioAsync`, `ICallAudioStream`, `ChannelHealth`,
  `SipAccountStatus`/`SipLineStatus`, `AudioFormat`) sind über Spec und Plan identisch.
