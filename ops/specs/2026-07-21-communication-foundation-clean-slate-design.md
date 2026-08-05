# Communication-Foundation (Clean-Slate) — Design-Spec

> Status: Design (Understanding gelockt inkl. API/WS-Pivot, wartet nicht mehr — Umsetzung läuft ab B0)
> Datum: 2026-07-21 (rev. API/WS-first)
> Nordstern: AiAgent (`/home/dbechstein/Downloads/ai-call-agent-plugin (1).md`)
> Verwandt: [[callora-communication-rebuild-2026-07]], ADR-012/REV2 §10.1, CODE_STRUCTURE_RULES.md

## 1. Ziel & Nicht-Ziele

**Ziel:** Clean-Slate-Neuaufbau des System-Tier-Plugins `custom/static-plugins/Communication`
als **Foundation** für Kommunikations-Anwendungen (Erstkunde AiAgent). Kein Softphone.

**Primäre Consumer-Fläche = API (Shopware-App-analog):** REST-Control + **Webhooks** für
Events + **WebSocket-Media-Stream** für Echtzeit-Audio (Twilio-Media-Streams-Stil). Damit
lassen sich Consumer — insbesondere AI-Voice-Agenten — **out-of-process in beliebiger
Sprache (Python/JS)** bauen, dort wo das STT/LLM/TTS-Ökosystem lebt.

**Sekundäre Fläche = in-process .NET-Contract (Abstractions):** dünnes Vertrags-Assembly für
tief integrierte .NET-Plugins (Flow-Actions, .NET-native Consumer). Bleibt erhalten, ist
aber nicht mehr der Default-Weg für Agenten.

**In-Scope (v1):**
- Voll ausgebauter **Voice-Channel** über CalloraVoipSdk 4.6 (Inbound + Outbound).
- **Media-Bridge:** SDK-RTP ↔ WebSocket; die Foundation trägt Pacing/Jitter/DTX (die
  schwache RTP/Media-Fläche der SDK, Agent-Doc §7) **an einer** Stelle — der externe Agent
  bekommt nur einen Audio-Chunk-Stream.
- **REST-Control-API** (Accounts/Lines/Calls/Webhooks/Stream-Sessions) + **Webhook-Dispatch**.
- **Domänen-/Persistenzmodell** `SipAccount → SipLine → Call/CallLog` + `WebhookSubscription`
  + `MediaStreamSession` (§4), DSGVO-konform.
- **In-process Abstractions** (sekundär) unter `src/Abstractions/` — eigenes Assembly.

**Nicht-Ziele (v1):**
- Kein Nicht-Voice-Channel (SMS/Messaging) — Abstraktion channel-ready, nur Voice gebaut.
- Kein Softphone-UI, keine Queue/IVR-Builder.
- Keine Recordings/Transkripte in der Foundation (nur Call-Metadaten).
- Keine Migration installierter Alt-Daten (Alt-Plugin wird verschoben/archiviert, §3).
- Keine Cloud-Lizenz-/Verwaltungsplattform (eigenes späteres System, `callora-store`).

## 2. Understanding-Summary (gelockt)

- **Was:** Communication-Foundation mit **API-first** (REST + Webhooks + WS-Media) als
  primärer Consumer-Fläche; in-process .NET-Abstractions als sekundärer tiefer Pfad.
- **Warum API-first:** AI-Ökosystem ist Python/JS; out-of-process ist sprachneutral,
  sandboxed (passt zu Cloud/Geschäftsmodell) und markttypisch (Twilio Media Streams,
  LiveKit, Pipecat, Vapi, Retell). REST allein trägt kein Echtzeit-Audio → WebSocket-Media.
- **Für wen:** externe Agenten (primär) + tief integrierte .NET-Plugins (sekundär).
- **Kernbeschränkung:** Clean-Slate inkl. Abstractions (bewusster Vertragsbruch); SDK 4.6
  über lokalen NuGet-Feed (verdrahtet).
- **AiAgent-Konsequenz:** wird eher ein **externer Python-Dienst** (oder dünnes .NET-Plugin
  mit Python-Sidecar), der REST+WS nutzt — nicht zwingend ein in-process-.NET-Plugin.

## 3. Verhältnis zum Alten & B0-Vorgehen (verschieben, nicht löschen)

- Das alte Plugin wird **verschoben, nicht gelöscht**: `git mv` von altem **Impl** + **Dialer**
  + ihren abhängigen **Testdateien** (inkl. der 3 Host-Infra-Fixture-Tests) nach
  `custom/static-plugins/_archive/…` — vollständig erhalten, aus dem aktiven Build/der Suite
  raus.
- Die **alte Abstractions bleibt in B0 an Ort und Stelle bauen** (die CLI referenziert sie
  für die ALC-Typidentität); **B1 ersetzt** sie durch die neue unter `src/Abstractions/` und
  hängt die CLI-Referenz um.
- Host-Infra-Coverage (Plugin-DB-Factory/-Migration/Curated-SP) fehlt übergangsweise und
  kommt mit B3 (Persistenz) zurück.
- Dialer wird archiviert; Re-Homing auf neue Verträge = Follow-up.

## 4. Domänenmodell

### 4.1 `SipAccount` — Provider-/Trunk-Verbindung
Wie gehabt: Host, Port, Transport (`Udp|Tcp|Tls`), `Mode` (`Register|Trunk`), Credentials →
`ISecretStore`, `MaxConcurrentCalls`, Media-/NAT-Optionen, `Enabled`.
**Status (`SipAccountStatus`):** `Disabled·Connecting·Up·Degraded·Failed` (+LastError, LastChangeAt).

### 4.2 `SipLine` — aufrufbare Identität unter einem Account
`AccountId`, `Label`, `SipUri`/`Aor`, `PrimaryNumber?` (DID v1 an der Line), `Enabled`,
`InboundRoutingTarget` (Flow-ID / Webhook-Subscription / Capability).
**Status (`SipLineStatus`):** `Disabled·Unavailable·Available·Busy` (abgeleitet).
**Monetarisierungs-Haken (später, nicht v1):** Line-Erstellung über einen **gate-baren**
Service — Cloud kann Line-Anzahl per Entitlement begrenzen, self-hosted unbegrenzt. Limit-/
Lizenz-Quelle liegt **außerhalb** (Cloud-/Store-Plattform), nicht Teil dieser Spec.

### 4.3 `Call` (dynamisch) + `CallLog` (History)
Laufzeit `IVoipCall`/`ICall`. `CallLog` (persistiert, DSGVO): `LineId`/`AccountId`,
`Direction`, `RemoteParty` (**sensitiveField**), `LocalIdentity`, Zeiten, `DurationSeconds`,
`Outcome`, `DisconnectCause`, `HandledBy`, `CorrelationId`. **Keine** Recordings/Transkripte.
Retention konfigurierbar (Default 90 Tage), `IWorkspaceDataPurgeContributor`.

### 4.4 `WebhookSubscription` — Consumer-Event-Abo (NEU, API-Fläche)
`WorkspaceKey`, `ConsumerName`, `Url`, `SigningSecretRef` (→ `ISecretStore`, HMAC),
`SubscribedEvents` (`call.ringing/answered/ended/dtmf/…`), `Enabled`. Zustellung mit
Signatur + Retry/Backoff über die bestehende `IBackgroundJobQueue`.

### 4.5 `MediaStreamSession` — WS-Stream-Bindung (NEU, API-Fläche)
Bindet einen lebenden Call an den WebSocket-Stream eines externen Consumers.
`CallId`, `WorkspaceKey`, `ConsumerRef`, `ConnectToken` (kurzlebig, einmalig, → Auth des WS),
`Format` (`AudioFormat`), `Direction`, `StartedAt`/`EndedAt`, `Status`
(`Pending·Active·Closed`). Nur Metadaten persistiert (kein Audio).

## 5. Primäre Fläche — Media-Streaming-API (Twilio-Media-Streams-Stil)

Drei Transporte, sauber getrennt nach Latenz-Anforderung:

### 5.1 REST-Control-API (`IHostAdminApiExtensionContributor`)
Request/Response, workspace-scoped, RBAC (`communication.*`-Permissions):
- Accounts/Lines: CRUD + Live-Status + Re-Register.
- Calls: `POST /communication/calls` (Outbound), `accept`/`reject`/`hangup`/`dtmf`,
  History (paginiert).
- Webhooks: CRUD der `WebhookSubscription`.
- Stream-Session: `POST /communication/calls/{id}/stream` → liefert `ConnectToken` + WS-URL.

### 5.2 Webhook-Events (out-of-process Push)
Bei `call.ringing` (Inbound) POST an die abonnierten URLs, HMAC-signiert; Payload enthält
Call-Metadaten **und** einen `ConnectToken`+WS-URL, mit dem der Consumer den Media-Stream
attacht (analog Twilios `<Connect><Stream>`). Weitere Events: `call.answered/ended/dtmf`,
`line.registered/failed`, `account.up/down`. Zustellung + Retry über `IBackgroundJobQueue`.

### 5.3 WebSocket-Media-Stream (`wss://…/communication/media/{connectToken}`)
Bidirektionaler Audio-Transport; JSON-Rahmen mit base64-µ-law (Twilio-kompatibles Schema):
- **inbound** (Server→Consumer): `{event:"media", payload:<b64 µlaw 20ms>}`
- **outbound** (Consumer→Server): `{event:"media", payload:<b64>}`
- **control:** `{event:"start", …call/format…}`, `{event:"stop"}`, `{event:"mark", name}`
  (Playback-Marker), `{event:"clear"}` (Outbound-Puffer flushen → **Barge-In**).
Der **Media-Bridge** (Infrastructure) übersetzt SDK-RTP ↔ WS, taktet Outbound präzise
(monotone Clock, kein `Task.Delay`), behandelt Jitter/DTX/Comfort-Noise. Der Consumer sendet
nur Chunks + `clear` bei Barge-In.

### 5.4 MCP — agenten-native Tool-Fläche (Callora-weit, Communication als Erstlieferant)
Zusätzlich zu REST bekommt Callora einen **MCP-Server** (Model Context Protocol) als
*standardisierte, LLM-native* Kontroll-/Tool-Fläche: jeder MCP-fähige Client (Claude Desktop,
der AiAgent, andere LLM-Apps) kann Callora-Fähigkeiten als **Tools** aufrufen.
- **Host-Ebene (eigene Plattform-Initiative):** MCP-Server-Framework + Contribution-Point
  (`IMcpToolContributor`, analog `IHostAdminApiExtensionContributor`), damit **Plugins** Tools
  beisteuern — wie heute REST-Endpoints/Flow-Actions/Events.
- **Communication als Erstlieferant:** Call-Control-Tools (`place_call`, `accept`, `hangup`,
  `get_call_status`, `list_call_history`) + Account/Line-Status als MCP-Resources.
- **Kein Media über MCP:** MCP ist Request/Response-Tool-Calling; Echtzeit-Audio bleibt der
  WebSocket. **MCP = was der Agent tut/liest · WS = Audio · Webhooks = Events.**
- **Architektur:** MCP, REST und WS sind **Adapter über denselben Application-Services** (DDD:
  transport-neutrale App-Schicht, Adapter außen) → kein Rework, MCP ist ein weiterer Adapter
  unter `Api/Mcp/`.
- **Sequenzierung:** Das Host-MCP-Framework ist eine **parallele Plattform-Initiative** (eigener
  Spec/Plan). Communication baut seine App-Services v1 transport-neutral, exponiert zunächst
  REST+WS+Webhooks; die MCP-Beisteuerung folgt, sobald das Host-Framework steht.

## 6. Sekundäre Fläche — in-process .NET-Contract (`src/Abstractions/`)

Eigenes dünnes Vertrags-Assembly (ALC-Typidentität) **unter `src/Abstractions/`**, damit am
Plugin-Root nur `registry.json` + csproj liegen. Für tief integrierte .NET-Plugins.

- **Behalten (channel-agnostisch):** `ICall`, `CallState/Direction/Target`,
  `CallStateChangedEventArgs`, `IncomingCallEventArgs`, `CallSummary`, `ICommunicationChannel`
  (+`ChannelHealth`), `ICommunicationChannelRegistry`, `CommunicationCapabilities`, Consent.
- **Voice-Erweiterung:** `IVoiceChannel : ICommunicationChannel`, `IVoipCall : ICall`
  (`OpenAudioAsync() → ICallAudioStream`), `ICallAudioStream` (`FrameReceived`+`SendAsync`),
  `AudioFormat`. Dieselbe Media-Bridge speist beide Flächen.
- **Verworfen:** `ICallEventStream/Subscription/CallStreamEvent` (→ Webhooks/Business-Events);
  `ICallDirectory` (→ REST/`PlaceCallAsync`).
- „Impl zusätzlich in den Default-Context laden?" bleibt eine **orthogonale** Hosting-Frage,
  entschieden in B4 — die separate Abstractions zwingt der Impl keinen ALC auf.

## 7. Architektur & Schichten (System-Tier, neues src/-Layout)

```
custom/static-plugins/Communication/
├── registry.json                        # capabilities ["communication.voice"]
├── Callora.Plugin.Communication.csproj   # kompiliert src/** OHNE src/Abstractions; refs Abstractions + Core + Analyzer + CalloraVoipSdk 4.6 + EF/Npgsql
└── src/
    ├── CommunicationPlugin.cs            # Composition Root
    ├── Abstractions/                     # eigenes Vertrags-csproj (sekundäre Fläche, §6)
    ├── Domain/{Accounts,Lines,Calls,Webhooks,Streaming}/
    ├── Application/
    │   ├── Accounts/ Lines/ Calls/       # Ports, Coordinators, InboundRouter
    │   ├── Streaming/                    # MediaSessionService, Barge-In-/Mark-Logik
    │   ├── Webhooks/                     # WebhookDispatcher (über IBackgroundJobQueue), Signatur
    │   └── Compliance/                   # Purge-Contributor, Retention
    ├── Infrastructure/
    │   ├── Sdk/                          # CalloraVoipSdk 4.6-Bridge (Registration, IVoipCall)
    │   ├── Media/                        # Media-Bridge SDK-RTP↔WS, G711/PCM, PacedSender, Jitter/DTX
    │   ├── Persistence/                  # CommunicationDbContext (plugin_communication), Migrations
    │   └── Transport/                    # WS-Handler, Webhook-HTTP-Client
    └── Api/{Rest,WebSocket,Mcp}/         # Präsentations-Adapter über dieselben Application-Services
```

**Code-Konventionen:** nach **DDD-Schicht UND Feature** sortiert (Feature-Ordner je Schicht,
z.B. `Application/Accounts`, `Domain/Streaming`), ein Typ pro Datei (CODE_STRUCTURE_RULES).
Qualität aktiv an den refactoring.guru-Katalogen: **Smells vermeiden** (Long Method, Large
Class, Feature Envy, Primitive Obsession, Data Clumps …), **Techniken anwenden** (Extract
Method/Class, Move Method, Replace Conditional with Polymorphism …). [[callora-code-conventions-ddd-feature-refactoring]]

**Media-Bridge = Kernrisiko** (SDK-RTP/Media ist die schwache Fläche, Agent-Doc §7): gekapselt
in `Infrastructure/{Sdk,Media}`, trägt Pacing/Jitter/DTX; Application/Domain SDK-frei.

## 8. Datenfluss (Inbound-Agent-Call, primärer Weg)

1. SDK meldet Inbound-Call auf einer Line → `Call` (Ringing), `CallLog` angelegt.
2. `InboundRouter` findet die `WebhookSubscription`(s) der Line → `WebhookDispatcher` POSTet
   `call.ringing` (signiert) inkl. `ConnectToken`+WS-URL.
3. Consumer (Python-Agent) antwortet/handelt: akzeptiert via REST (`accept`) und öffnet den
   **WebSocket** mit dem `ConnectToken` → `MediaStreamSession` wird `Active`.
4. **Media-Bridge**: inbound RTP → G.711-Frames → WS (`media`); outbound WS-`media` → präzise
   getaktet → SDK-RTP. `clear` vom Consumer flusht den Outbound-Puffer (**Barge-In**).
5. `hangup`/SDK-Ende → `call.ended`-Webhook, `MediaStreamSession` `Closed`, `CallLog`
   finalisiert.

*(Deep-Path .NET-Consumer nutzen statt Schritt 2–4 direkt `IVoipCall.OpenAudioAsync()`.)*

## 9. Persistenz & DSGVO
`CommunicationDbContext` (Schema `plugin_communication`) über `IPluginDbContextFactory<T>`;
Tabellen `sip_accounts`, `sip_lines`, `call_logs`, `webhook_subscriptions`,
`media_stream_sessions`. Secrets (SIP-Passwörter, Webhook-Signing) nur als `…SecretRef` →
`ISecretStore`. `CommunicationDataPurgeContributor` (RemoteParty anonymisieren, Logs kürzen).
Consent: Recording nur bei `RecordingConsentState.Granted` (Recording ist ohnehin
Consumer-Sache).

## 10. Sicherheit
- **WS-/Webhook-Auth:** `ConnectToken` kurzlebig, einmalig, call-gebunden; Webhooks
  HMAC-signiert (Consumer verifiziert). Kein Dauer-Secret im WS-URL.
- **Least-Privilege:** REST-API RBAC-/workspace-scoped; Consumer sehen nur ihre Calls/Lines.
- **Out-of-process = Sandbox:** externe Agenten laufen außerhalb des Hosts (kein ALC-Zugriff).

## 11. Fehlerbehandlung & Resilienz
Registrierungsfehler → `Account.Status=Failed` + Event, kein Crash (SDK-Backoff). Webhook-
Zustellung mit Retry/Backoff (Job-Queue). WS-Abbruch → Session `Closed`, Call bleibt (Consumer
kann re-attachen, solange Call lebt). Concurrency-Limit → definierte Ablehnung.

## 12. Teststrategie
Contract-Tests der Abstractions (State-Machine, Registry, Audio-Duplex). WS-Protokoll-Tests
(Frame-Roundtrip, `clear`/Barge-In, Auth via `ConnectToken`). Webhook-Signatur + Retry.
Media-Bridge gegen Loopback/Fake-SDK. EF-Persistenz + Purge. Governance-Analyzer CAL0001–4.

## 13. Sequenzierung (Bausteine) & Follow-ups

| B | Inhalt | Status |
|---|---|---|
| **B0** | Grund freiräumen: Alt-Impl+Dialer+abh. Tests **verschieben**; Alt-Abstractions bleibt bauen | **läuft** |
| **B1** | Neue Abstractions unter `src/Abstractions/` (Vertrag + Contract-Tests); CLI-Ref umhängen | Plan |
| **B2** | Media-Streaming-API-Skelett: REST-Control + WS-Endpoint + Webhook-Modelle (ohne echtes SDK-Audio) | Plan |
| **B3** | Domain + Persistenz (Accounts/Lines/CallLog/Webhooks/Sessions, DbContext, Migration, Purge) | Plan |
| **B4** | SDK-Bridge + Media-Bridge (SDK-RTP↔WS, Pacing/Jitter), Registration-Lifecycle *(SDK-4.6-API-Discovery)* | Plan |
| **B5** | Inbound/Outbound + Webhook-Dispatch + Barge-In + CallLog-Finalisierung; Flows/Consent | Plan |
| **B6** | Admin-Surface (optional) + Härtung; Dialer-Entscheidung | Plan |

**Follow-ups:** Dialer re-homen/verwerfen; SDK-Feed für CI; Number/DID-Entity; Impl-in-
Default-ALC-Frage; Cloud-Line-Limit (extern).

## 14. Entscheidungslog (Ergänzungen zur API/WS-Wende)

| Entscheidung | Alternative | Warum |
|---|---|---|
| **API/WS-first** (REST+Webhooks+WS-Media), in-process sekundär | nur in-process .NET-Contract | AI-Ökosystem ist Python/JS; out-of-process sprachneutral+sandboxed; Twilio/LiveKit/Pipecat-Standard; Media-Härtung bleibt in der Foundation |
| WebSocket für Audio (Twilio-Media-Streams-Schema) | REST / gRPC | REST trägt keine 20-ms-Frames; WS ist Marktstandard + Twilio-kompatibles Payload |
| Abstractions bleibt separat, unter `src/Abstractions/` | in Impl-Assembly mergen | ALC-Typidentität + dünner Compile-Vertrag ohne SDK/EF-Ballast; Root-Sauberkeit |
| Alt-Plugin verschieben statt löschen (inkl. Tests) | git rm | nichts geht verloren; History bleibt; Coverage kehrt mit B3 zurück |
| SipAccount→SipLine→Call/CallLog, Status getrennt | Line=DID / Slot | an realen SIP-Systemen geerdet (siehe Vorgänger-Revision) |

## 15. Annahmen
- Audio v1 = G.711 µ-law 8 kHz/20 ms; Base64-JSON-WS-Frames (Twilio-kompatibel).
- `ConnectToken` einmalig/kurzlebig; Webhook-HMAC über `SigningSecretRef`.
- Retention-Default 90 Tage; `WorkspaceKey` = Mandantenachse.
