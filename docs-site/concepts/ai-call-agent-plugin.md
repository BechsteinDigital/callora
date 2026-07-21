# AI Call Agent Plugin — Konzept & Architektur

> Status: Konzept / Design (noch keine Implementierung)
> Zielbild: Ein Plugin, mit dem Callora-Betreiber eigene KI-Telefonagenten
> bauen und betreiben können — als erstes Beispiel ein Support-/Bestell-Agent
> für den Shopware-Shop **kennzeichenheld.de**.

Dieses Dokument beantwortet drei Fragen:

1. **Wie sind typische AI-Call-Agenten aufgebaut?** (Stand der Technik)
2. **Was hat Callora heute schon** und **was fehlt** für so einen Agenten?
3. **Wie sieht der konkrete Aufbau** (Plugin-Architektur, Datenfluss,
   Shopware-Anbindung, Compliance) aus?

---

## 1. Wie ein typischer AI-Call-Agent aufgebaut ist

Ein Sprach-Agent ist im Kern eine **Echtzeit-Audio-Schleife** um drei Modelle:
Speech-to-Text (STT) → Large Language Model (LLM) → Text-to-Speech (TTS).
Um diese drei herum liegt die eigentliche Ingenieurskunst: Turn-Detection,
Barge-In, Tool-Calling und Latenz-Budget.

### 1.1 Die Pipeline

```
                        ┌──────────────────────────────────────────────┐
   Anrufer (PSTN/SIP)   │                 Voice Agent                   │
        │  G.711 20ms    │                                              │
        ▼  Frames        │   ┌─────┐   ┌─────┐   ┌─────────┐  ┌──────┐  │
   ┌─────────┐  inbound  │   │ VAD │──▶│ STT │──▶│   LLM   │─▶│ TTS  │  │
   │  Media  │──────────▶│──▶│ +   │   │(ASR)│   │ + Tools │  │      │  │
   │ (RTP)   │           │   │Turn │   └─────┘   └────┬────┘  └──┬───┘  │
   │         │◀──────────│   │Det. │                  │ Function │      │
   └─────────┘  outbound │   └─────┘                  │  Calls   │      │
        ▲   G.711 Frames │                            ▼          │      │
        │                │                    ┌───────────────┐  │      │
        └────────────────│◀───────────────────│ Backend/APIs  │◀─┘      │
                         │   synthetisiertes   │ (z.B. Shopware)│        │
                         │   Audio             └───────────────┘        │
                         └──────────────────────────────────────────────┘
```

**Die sechs Stufen:**

| Stufe | Aufgabe | Typische Anbieter |
|-------|---------|-------------------|
| **Audio In** | RTP/Media-Frames empfangen (bei uns G.711 8 kHz, 20 ms) | — (VoIP-SDK) |
| **VAD / Turn-Detection** | Erkennen, *wann* der Anrufer zu Ende gesprochen hat | Silero VAD (OSS), LiveKit/Pipecat Semantic-Turn |
| **STT (ASR)** | Audio → Text, möglichst *streaming* (Teiltranskripte) | Deepgram Nova, Whisper, Azure Speech, AssemblyAI |
| **LLM** | Verstehen, Dialogführung, Entscheidung, **Tool-Calls** | Claude (Anthropic), GPT, Gemini |
| **TTS** | Antworttext → natürliche Sprache, *streaming* | ElevenLabs, Cartesia Sonic (~100 ms), Azure, Deepgram Aura |
| **Audio Out** | Synthetisiertes Audio zurück in den Call (G.711) | — (VoIP-SDK) |

### 1.2 Zwei Architektur-Varianten

- **Sequenzielle Pipeline (Turn-based):** jede Stufe läuft vollständig, dann
  die nächste. Einfach, aber 2–4 s Antwortverzögerung → wirkt unnatürlich.
- **Streaming-Pipeline:** Stufen überlappen. STT schickt Teiltranskripte an
  das LLM, während der Anrufer noch spricht; das LLM streamt Tokens an TTS,
  sobald der erste Satz steht; TTS beginnt sofort zu sprechen. Das senkt die
  *gefühlte* Latenz um das 3–5-fache. **Das ist der Zielzustand.**
- **Realtime-/Speech-to-Speech-Modelle** (z. B. OpenAI Realtime) fassen
  STT+LLM+TTS in *ein* Modell und erreichen die niedrigste Latenz, kosten aber
  Flexibilität (Modell-/Anbieter-Lock-in, schwerer auditierbar). Callora sollte
  **beide** Betriebsarten als austauschbare Strategie unterstützen (siehe §3.4).

### 1.3 Die harten Probleme (jenseits von "3 APIs verketten")

- **Barge-In:** Der Anrufer unterbricht den sprechenden Agenten. Turn-Detection
  muss **auch während der TTS-Ausgabe aktiv** bleiben; erkannte Sprache muss die
  laufende TTS-Wiedergabe sofort abbrechen ("flush"). Budget: < 150 ms von
  Sprech-Ende bis TTS-Stopp.
- **End-of-Turn:** VAD allein reicht nicht (Pausen ≠ Satzende). Produktiv nutzt
  man VAD + Endpointing + semantische Turn-Detection.
- **Latenz-Budget:** Ziel ist "menschlich" ~500–800 ms von Sprech-Ende bis
  erste Agenten-Silbe. Jede Stufe hat ihr Teilbudget; alles muss streamen.
- **Tool-Calling / Grounding:** Der Agent darf nicht halluzinieren, sondern
  Fakten über **Function-Calls** aus echten Systemen holen (Bestellstatus,
  Sendungsverfolgung). Das ist der Teil, der aus einem Chatbot einen nützlichen
  Agenten macht.
- **Interruptibility & State:** Dialogzustand, "der Agent hat gerade X gesagt",
  DTMF-Eingaben, Weiterleitung an einen Menschen (Handover).
- **Compliance:** Ansage "Dieses Gespräch wird von einem KI-Assistenten
  geführt", Einwilligung bei Aufzeichnung, Datensparsamkeit.

---

## 2. Was Callora heute schon hat (und was fehlt)

Der große Vorteil: Callora bringt bereits die komplette **Telefonie- und
Plugin-Infrastruktur** mit. Ein AI-Agent ist kein neues System, sondern ein
neuer **Consumer der bestehenden Audio- und Flow-Ports**.

### 2.1 Vorhandene Bausteine (wiederverwendbar)

| Baustein | Ort | Rolle für den Agenten |
|----------|-----|-----------------------|
| **VoIP-SDK** (SIP/RTP/Codecs) | `callora-voip-sdk` | PSTN/SIP-Anbindung, G.711/G.722/Opus |
| **Communication-Plugin** (System-Tier) | `custom/static-plugins/Communication` | stellt Calls, Audio-Streams, Flows bereit |
| **`IVoipCall.OpenAudioAsync()`** | `.../Channels/IVoipCall.cs` | **der zentrale Andockpunkt** — bidirektionaler Audio-Stream |
| **`ICallAudioStream`** | `.../Audio/ICallAudioStream.cs` | `FrameReceived`-Event (inbound) + `SendAsync` (outbound), G.711-Frames |
| **`AnnouncementStreamer`** | `.../Audio/AnnouncementStreamer.cs` | Referenz: wie man 20-ms-G.711-Frames getaktet einspielt |
| **`G711Codec`, `PcmWaveReader`** | `.../Audio/` | Encode/Decode G.711 ↔ PCM16 |
| **`ICall` / `CallState` / `CallDirection`** | `Abstractions/src/Calls` | Call-Lebenszyklus, Accept/Reject/Hangup/DTMF |
| **Flow-Actions (`IFlowActionHandler`)** | `.../Flows/*`, `Core/.../Flows` | `call.accept`, `audio.play`, `call.hangup` — Agent wird eine neue Action |
| **Business-Events** (`call.ringing`, `call.ended`, …) | `Abstractions/.../CallEventTypes.cs` | Trigger, um den Agenten bei eingehendem Anruf zu starten |
| **`IRecordingConsentCall`** | `Abstractions/src/Consent` | DSGVO-konforme Einwilligung vor Aufzeichnung |
| **`ISecretStore` / `IPluginDataProtector`** | `Core/Application/Secrets` | **API-Keys** (OpenAI/Deepgram/ElevenLabs) sicher ablegen |
| **`IPluginConfigReader`** | `Core/Application/Configuration` | Agenten-Konfiguration (Prompt, Stimme, Modell) |
| **`IPluginDbContextFactory` / `IPluginDataStore`** | `Core/Application/{Persistence,Data}` | eigenes Schema für Agenten, Transkripte, Sessions |
| **`IBackgroundJobQueue`** | `Core/Application/Jobs` | Nachbearbeitung (Zusammenfassung, CRM-Sync) |
| **`IHostAdminApiExtensionContributor`** | `Core/Application/Plugins` | Admin-REST-API des Plugins (Agenten CRUD) |
| **Plugin-Signing, RBAC, Workspaces, Media-Library** | Host | Sicherheit, Mandanten, Audio-Assets |
| **Compliance-Baseline (DSGVO / EU AI Act)** | `docs/compliance`, `ops/compliance` | Rahmen ist gesetzt |

**Kernaussage:** Der Audiopfad ist fertig. `IVoipCall.OpenAudioAsync()` liefert
`ICallAudioStream` mit einem `FrameReceived`-Event (eingehende G.711-Frames) und
`SendAsync` (ausgehende Frames). Genau dort klemmt sich der Agent ein — analog
zu `AnnouncementStreamer`, nur dass die Frames nicht aus einer WAV-Datei, sondern
aus der STT→LLM→TTS-Schleife kommen.

### 2.2 Was fehlt (das ist das neue Plugin)

1. **Media-Bridge / Realtime-Orchestrator:** die Schleife, die inbound-Frames
   nach STT schiebt und TTS-Frames zurück auf `SendAsync` legt — inkl.
   Jitter/Pacing (20 ms), Barge-In-Flush, Turn-Detection.
2. **Provider-Abstraktionen (Ports):** `ISpeechToText`, `ILanguageModel`,
   `ITextToSpeech`, `ITurnDetector` — plus Adapter (Deepgram, Anthropic,
   ElevenLabs, …). Anbieter müssen austauschbar sein.
3. **Agent-Runtime & Dialog-State:** System-Prompt, Konversationsverlauf,
   Tool-Registry, Handover-Logik, Zustandsmaschine (greeting → serve → close).
4. **Tool-/Function-Calling-Framework:** deklarative Tools, die das LLM aufrufen
   darf (`getOrderStatus`, `getTracking`, `handoverToHuman` …), inkl.
   Shopware-Adapter.
5. **Agent-Definition & Admin-UI:** Agenten anlegen/konfigurieren (Prompt,
   Stimme, Modell, Tools, Öffnungszeiten) — Vue-Modul im Admin-Shell.
6. **Persistenz:** Agenten-Definitionen, Call-Sessions, Transkripte,
   Tool-Call-Audit.
7. **Compliance-Hooks:** KI-Offenlegungsansage, Consent-Kopplung, Redaction.

---

## 3. Vorgeschlagener Aufbau des Plugins

### 3.1 Verortung & Namen

Ein **dynamisch installierbares Plugin** unter `custom/plugins/AiAgent` (kein
System-Tier), das von `Callora.Plugin.Communication.Abstractions` abhängt — genau
wie das `Dialer`-Referenzplugin.

```
custom/plugins/AiAgent/
├── registry.json                      # pluginId "ai-agent", requiresCapabilities: ["communication.voice"]
├── Callora.Plugins.AiAgent.csproj
└── src/
    ├── AiAgentPlugin.cs               # Composition Root (IHostManagedPlugin)
    ├── Domain/
    │   ├── Agents/                    # AgentDefinition, AgentPersona, VoiceProfile (framework-frei)
    │   ├── Conversations/            # ConversationTurn, DialogState, TranscriptEntry
    │   └── Tools/                     # ToolDefinition, ToolCall, ToolResult
    ├── Application/
    │   ├── Runtime/                   # AgentSession, RealtimeOrchestrator, TurnManager, BargeInController
    │   ├── Speech/                    # Ports: ISpeechToText, ITextToSpeech, ITurnDetector
    │   ├── Reasoning/                 # Ports: ILanguageModel, IToolRegistry, ToolDispatcher
    │   ├── Media/                     # AudioBridge (ICallAudioStream <-> PCM), Resampler, Jitter/Pacer
    │   ├── Flows/                     # AgentAnswerActionHandler : VoipCallFlowActionHandlerBase
    │   ├── Events/                    # CallRinging-Listener -> Agent-Start
    │   └── Admin/                     # REST: Agenten-CRUD, Test-Call, Transkripte
    ├── Infrastructure/
    │   ├── Speech/                    # DeepgramStt, ElevenLabsTts, ... (Adapter)
    │   ├── Reasoning/                 # AnthropicLanguageModel, OpenAiLanguageModel (Adapter)
    │   ├── Tools/Shopware/            # ShopwareOrderTool, ShopwareTrackingTool, ...
    │   └── Persistence/               # AiAgentDbContext, Configurations/, Migrations/
    └── Resources/app/administration/  # Vue-Admin-Modul (Agenten verwalten)
```

Die Struktur spiegelt bewusst `CODE_STRUCTURE_RULES.md`
(Domain → Application → Infrastructure → Api, ein Typ pro Datei, Ports in
`Application`, Adapter in `Infrastructure`, Verdrahtung nur im Composition Root).

### 3.2 Wie der Agent an einen Call kommt (zwei Einstiegspunkte)

**A) Über einen Flow (empfohlen, konsistent mit der Plattform):**
Das Plugin exportiert eine neue Flow-Action `agent.answer` — genau wie
`audio.play` von `VoipCallFlowActionHandlerBase` abgeleitet. Der Betreiber baut
im Flow-Editor:

```
on call.ringing  →  [call.accept]  →  [agent.answer  agentId=…]
```

`AgentAnswerActionHandler` holt sich über den `VoipCallHub` den lebenden Call,
ruft `IVoipCall.OpenAudioAsync()` und startet die `AgentSession`. Vorteil: nutzt
das bestehende Flow-/Trigger-/Bedingungssystem (Öffnungszeiten via
`TimeWindowCondition`, Nummern-Routing via `DataFieldCondition`).

**B) Über einen Business-Event-Listener** (`call.ringing`) für vollautomatische
"immer annehmen"-Agenten ohne Flow.

### 3.3 Der Realtime-Datenfluss (Herzstück)

```
IVoipCall.OpenAudioAsync() ── ICallAudioStream
        │  FrameReceived (G.711 20ms)
        ▼
 ┌───────────────┐   PCM16 8k/16k     ┌──────────────┐  partial text  ┌──────────────┐
 │  AudioBridge  │──── decode ───────▶│ ISpeechToText │───────────────▶│ TurnManager  │
 │ (G711<->PCM,  │                    │  (streaming)  │                │ + ITurnDetect│
 │  resample,    │                    └──────────────┘                └──────┬───────┘
 │  jitter/pace) │                                                            │ end-of-turn
 │               │◀── encode ◀── PCM ◀─┐                                      ▼
 └───────┬───────┘                     │                            ┌──────────────────┐
         │ SendAsync (G.711 20ms)      │                            │  ILanguageModel  │
         ▼                             │                            │  (streaming) +   │
   Anrufer hört Agent          ┌───────┴──────┐  audio chunks       │  IToolRegistry   │
                               │ ITextToSpeech │◀── token stream ────│  Tool-Calls ─────┼──▶ Shopware
         Barge-In: ◀───────────│  (streaming)  │                     └──────────────────┘   (getOrder…)
         FrameReceived erkennt  └──────────────┘
         Sprache während TTS  ─── BargeInController.Flush() ──▶ TTS abbrechen, LLM-Turn verwerfen
```

Konkret in Callora-Begriffen:

1. `AgentAnswerActionHandler` erhält den `IVoipCall`, ruft `OpenAudioAsync()`.
2. `AudioBridge` abonniert `FrameReceived`, **dekodiert G.711 → PCM16** (via
   `G711Codec`), resampled auf die STT-Rate, puffert (die Doku warnt: Handler
   dürfen **nicht blockieren** → in Queue schreiben, sofort zurück).
3. `ISpeechToText` (z. B. Deepgram über WebSocket) liefert Teiltranskripte.
4. `TurnManager` + `ITurnDetector` entscheiden End-of-Turn.
5. `ILanguageModel` (Anthropic/OpenAI) bekommt Prompt + Verlauf, **streamt
   Tokens** und ggf. **Tool-Calls**; `ToolDispatcher` führt sie aus (Shopware).
6. `ITextToSpeech` synthetisiert satzweise, `AudioBridge` **encodet PCM → G.711**
   und spielt die Frames **getaktet (20 ms)** über `SendAsync` ein — exakt das
   Muster aus `AnnouncementStreamer`.
7. `BargeInController` lauscht parallel auf `FrameReceived`: erkennt VAD Sprache
   während der Agent spricht → **TTS-Puffer verwerfen + laufenden LLM-Turn
   abbrechen**.

### 3.4 Provider-Ports (austauschbar, Secrets-gestützt)

```csharp
// Application/Speech/ISpeechToText.cs
public interface ISpeechToText : IAsyncDisposable
{
    // liefert Teil-/Endtranskripte als Stream
    IAsyncEnumerable<TranscriptChunk> TranscribeAsync(
        ChannelReader<PcmFrame> audio, CancellationToken ct);
}

// Application/Reasoning/ILanguageModel.cs
public interface ILanguageModel
{
    IAsyncEnumerable<ModelDelta> CompleteAsync(
        Conversation conversation,
        IReadOnlyList<ToolDefinition> tools,
        CancellationToken ct); // ModelDelta = Text-Token ODER ToolCall
}

// Application/Speech/ITextToSpeech.cs
public interface ITextToSpeech : IAsyncDisposable
{
    IAsyncEnumerable<PcmFrame> SynthesizeAsync(
        IAsyncEnumerable<string> textStream, VoiceProfile voice, CancellationToken ct);
}
```

- Adapter liegen in `Infrastructure/…`, API-Keys kommen aus `ISecretStore`.
- Eine **Speech-to-Speech-Strategie** (OpenAI Realtime) implementiert dieselbe
  `AgentSession`-Schnittstelle, ersetzt aber STT+LLM+TTS durch *einen* Duplex-
  Stream — die `AudioBridge` bleibt identisch.
- Default-Empfehlung für Latenz/Kosten: **Deepgram Nova (STT) + Claude (LLM,
  Tool-Use) + ElevenLabs/Cartesia (TTS)**, alle im Streaming-Modus.

### 3.5 Datenmodell (`AiAgentDbContext`, eigenes Schema `plugin_ai_agent`)

- `AgentDefinition` — Name, System-Prompt/Persona, Modell-/Stimm-Wahl,
  Tool-Whitelist, Öffnungszeiten, Fallback-/Handover-Nummer, Sprache.
- `AgentToolBinding` — welche Tools mit welcher Konfiguration (z. B.
  Shopware-Sales-Channel, Basis-URL).
- `CallSession` — Verknüpfung Call ↔ Agent, Start/Ende, Outcome, Kosten.
- `TranscriptEntry` — Turn-für-Turn Transkript (mit Redaction-Flag).
- `ToolCallLog` — Audit: welches Tool mit welchen Args, Ergebnis (EU-AI-Act).

### 3.6 Admin-UI (Vue-Modul)

Micro-Frontend im Admin-Shell (wie im README beschrieben): Agenten-Liste,
Agent-Editor (Prompt, Stimme, Modell, Tools, Öffnungszeiten), Secrets-Verwaltung
(Provider-Keys), Transkript-Viewer, **Test-Call-Button**. RBAC-Permission-Keys
analog `VoipPermissionKeys`.

---

## 4. Der Anwendungsfall: kennzeichenheld.de (Shopware)

kennzeichenheld.de ist ein **Shopware-Shop** (Kfz-Kennzeichen-Konfigurator).
Ein Telefon-Agent bringt dort v. a. Wert bei **Bestellstatus-, Versand- und
Produktfragen** sowie bei der **Bestellaufnahme/-korrektur** — klassische
Entlastung des telefonischen Kundenservice.

### 4.1 Anbindung: Shopware als "Werkzeugkasten" des Agenten

Der Agent selbst kennt Shopware nicht — er ruft **Tools** (Function-Calls) auf,
die im `Infrastructure/Tools/Shopware`-Adapter gegen die **Shopware-APIs** gehen:

- **Admin API** (`/api`, OAuth Client-Credentials) für Bestell-/Kundendaten:
  Bestellung per Bestellnummer + Verifikation (PLZ/E-Mail) suchen, Status,
  Sendungsnummer, Positionen.
- **Store API** (`sales-channel-api`, Access-Key) für Katalog/Verfügbarkeit,
  wenn der Agent produktbezogen antworten soll.

Empfohlene Start-Tools:

| Tool | Zweck | Shopware-Quelle |
|------|-------|-----------------|
| `identifyCustomer` | Anrufer verifizieren (Bestellnr. + PLZ/E-Mail) | Admin API `order` search |
| `getOrderStatus` | Zahlungs-/Lieferstatus einer Bestellung | Admin API `order`, `state_machine_state` |
| `getTrackingInfo` | Sendungsnummer + Link | Admin API `order_delivery.trackingCodes` |
| `getProductInfo` | Produkt/Preis/Verfügbarkeit, Kennzeichen-Optionen | Store API `product` |
| `createSupportTicket` | Anliegen erfassen, wenn nicht lösbar | Callora-Flow/Webhook/Mail |
| `handoverToHuman` | An Mitarbeiter/Warteschlange durchstellen | `ICall` Transfer / Dialer |

Jedes Tool ist eine `ToolDefinition` (Name, JSON-Schema der Parameter,
Beschreibung fürs LLM) + ein Handler. Der `ToolDispatcher` erzwingt die
**Whitelist pro Agent** und loggt jeden Aufruf (`ToolCallLog`).

### 4.2 Beispiel-Dialog (was passiert intern)

> **Anrufer:** "Ich wollte fragen, wo meine Kennzeichen bleiben, Bestellung 100245."
> — STT-Teiltranskript → LLM erkennt Intent "Bestellstatus", ruft
> `getOrderStatus(orderNumber:"100245")` → Tool fragt zur Verifikation PLZ ab →
> LLM: "Zur Sicherheit — welche Postleitzahl steht auf der Bestellung?" (TTS)
> **Anrufer:** "50667." → `identifyCustomer` + `getTrackingInfo` →
> LLM: "Ihre Kennzeichen sind gestern per DHL rausgegangen, Sendungsnummer …,
> Zustellung voraussichtlich morgen." (TTS)

### 4.3 Warum das gut zu Callora passt

- Der Shopware-Adapter ist **nur ein weiterer Tool-Provider** — andere Betreiber
  bringen ihre eigenen Tools (CRM, ERP) mit, der Agent-Kern bleibt gleich.
- Die Anbindung ist **entkoppelt**: Shopware wird über HTTP von der
  Infrastructure-Schicht angesprochen, nicht vom Domain-Kern (Clean Architecture,
  konform zu den Schichtregeln).

---

## 5. Compliance (DSGVO / EU AI Act)

Callora hat bereits eine Compliance-Baseline — der Agent muss sie erfüllen:

- **KI-Offenlegung (EU AI Act, Transparenzpflicht):** verpflichtende Ansage zu
  Gesprächsbeginn ("Sie sprechen mit einem KI-Assistenten") — als erster
  TTS/`audio.play`-Schritt, nicht abschaltbar.
- **Einwilligung bei Aufzeichnung:** an das vorhandene `IRecordingConsentCall`
  koppeln — Transkripte/Recording nur bei `RecordingConsentState.Granted`.
- **Datensparsamkeit & Redaction:** Transkripte pseudonymisieren; sensible Felder
  über `sensitiveFields` (wie im Communication-`registry.json`) markieren; an
  `IWorkspaceDataPurgeContributor` für Löschkonzepte anschließen.
- **Auditierbarkeit:** `ToolCallLog` + Transkripte machen Agentenentscheidungen
  nachvollziehbar.
- **Human-Handover:** jederzeit "mit einem Mitarbeiter sprechen" → Transfer.
- **Provider-Datenfluss:** STT/LLM/TTS-Anbieter sind Auftragsverarbeiter →
  EU-Region/AVV beachten; Anbieterwahl ist konfigurierbar (Ports!).

---

## 6. Umsetzungs-Roadmap (Vorschlag)

| Phase | Inhalt | Ergebnis |
|-------|--------|----------|
| **P0 – Skeleton** | Plugin `ai-agent`, `registry.json`, Composition Root, DbContext, Admin-CRUD (ohne Audio) | Agenten anlegbar |
| **P1 – Echo/Bridge** | `AudioBridge`: inbound-Frames aufnehmen, sofort wieder ausspielen (Loopback) | Audiopfad bewiesen |
| **P2 – STT+TTS** | Deepgram-STT + ElevenLabs-TTS, feste Begrüßung, Transkript in DB | Agent "hört & spricht" |
| **P3 – LLM-Dialog** | `ILanguageModel` (Claude), Streaming, Turn-Detection, Barge-In | echtes Gespräch |
| **P4 – Tools/Shopware** | Tool-Framework + Shopware-Adapter (`getOrderStatus`, `getTracking`) | nützlicher Support-Agent |
| **P5 – Flow+Admin+Compliance** | `agent.answer`-Flow-Action, Consent-Ansage, Admin-UI, Handover | produktionsreif |
| **P6 – Realtime-Option** | Speech-to-Speech-Strategie (OpenAI Realtime) als Alternative | niedrigste Latenz |

**Empfohlener nächster Schritt:** P0-Skeleton als lauffähiges Plugin-Gerüst
(analog `Dialer`) anlegen, damit die Struktur steht — darauf inkrementell P1–P4.

---

## Quellen (Recherche AI-Voice-Agents)

- LiveKit — Voice Agent Architecture: STT/LLM/TTS Pipelines; Turn Detection (VAD, Endpointing, Model-based)
- Softcery — Real-Time vs Turn-Based Voice Agents 2026 (Architektur, Latenz, Kosten)
- FutureAGI — Voice AI Barge-In & Turn-Taking Implementation Guide 2026
- Shopware Developer Docs — Store API / Admin API / Integrations
</content>
</invoke>
