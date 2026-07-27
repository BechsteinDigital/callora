# Communication Call-Control — Design

**Datum:** 2026-07-27
**Status:** akzeptiert (Kontur bestätigt), Umsetzung in Slices
**Kontext:** Die Voice-Audio-Bridge (B4-deep) ist fertig und grün gegen Asterisk. Was fehlt, ist die
**Steuerungsebene** um das Medium — die Primitive, auf denen die künftigen Ökosystem-Plugins
(Dialer, PBX, CRM, AI-Voice-Agent) aufsetzen.

## Understanding

- **Was:** Ein channel-neutraler Call-Control-Primitive im Communication-System-Plugin: Calls
  platzieren/auflegen/abfragen, Call-Historie persistieren, `call.*`-Business-Events publizieren.
- **Warum:** Downstream-Plugins sollen `channel.PlaceCallAsync(...)` bzw. den Service konsumieren
  und `call.answered` hören — statt je ein RTP-Paket anzufassen. Wir nehmen ihnen die harte,
  plattform-privilegierte Arbeit ab (SIP/SDK/RTP), **nicht** die Domäne.
- **Für wen:** In-process .NET-Plugins (Dialer/PBX/CRM) via DI **und** out-of-process Consumer
  (externer AI-Agent, Webhooks, später MCP) via REST.
- **Kernprinzip:** **Ein Primitive-Service, mehrere Gesichter.** `ICallControlService` ist die
  einzige Wahrheit; REST, Events/Webhooks und später MCP sind dünne Adapter darüber.

### Non-Goals (bewusst NICHT hier — eigene Plugins/spätere Slices)

- Inbound-**Routing-Engine** (IVR, Queues, Ring-Groups, Routing-Baum) → das **ist** eine PBX.
  Communication liefert nur „Call kam rein"-Event + `Accept/Reject/Hangup`.
- Dialer-Kampagnen/Pacing/Retry/DNC, CRM-Sync, AI-Agent-Orchestrierung.
- MCP-Tools (späterer dünner Adapter), Trunk-Konnektivität (eigener Slice, braucht SDK-Support).

## Architektur

```
        ICallControlService  ← Primitive (Application-Schicht)
        Place / Hangup / Get / (List)
        nutzt: ICommunicationChannelRegistry · ICallLogStore · IBusinessEventBus
             ▲              ▲                     ▲
   In-process DI      REST-Adapter        call.* Business-Events
   (Dialer/PBX/CRM)   (externe Agenten)   → Core-Webhook-Zustellung (out-of-proc)
             ▲                             → in-process-Abonnenten am Bus
        MCP-Adapter (später)
```

### Vorhandene Seams (nichts neu zu bauen)

| Zweck | Contract | Ort |
|---|---|---|
| Channel pro Workspace auflösen | `ICommunicationChannelRegistry.TryGetChannel / GetChannelsByCapability` | Abstractions/Channels |
| Call platzieren/auflegen | `ICommunicationChannel.PlaceCallAsync` → `ICall` (Accept/Reject/Hangup/StateChanged) | Abstractions |
| Call-Historie | `ICallLogStore` + `CallLog.Start/MarkAnswered/End` | Application/Calls, Domain/Calls |
| Business-Events | `IBusinessEventBus.PublishAsync(IBusinessEvent)` | src/Core/Events |
| Event-Namen | `CallEventTypes` (call.ringing/placed/state-changed/ended) | Abstractions/Calls |
| Permission | `CommunicationPermissionKeys.CallsRead` (+ neu `CallsManage`) | Application/Admin |
| Admin-Route-Stil | `HostAdminApiRouteRegistration` + `IHostAdminApiRouteHandler`, Scope via `SipAccountAdminScope`-Muster | Application/Admin |

## Slices

### Slice 1a — Outbound Call-Control + Lifecycle (in-process)  ← zuerst
- `ICallControlService` (Port) + `CallControlService` (Impl): `PlaceCallAsync`, `HangupAsync`,
  `Get` + interne Active-Call-Registry (callId → getrackter `ICall`, workspace-scoped).
- Lifecycle: abonniert `ICall.StateChanged`. `Connected` → `CallLog.MarkAnswered` + Update +
  `call.state-changed`; `Terminated` → `CallLog.End(outcome)` + Update + `call.ended` + Untrack.
- Beim Platzieren: `CallLog.Start(Outbound)` + Add + `call.placed`.
- `CallBusinessEvent : IBusinessEvent` (Vorlage: `MediaBusinessEvent`) mit `ToEventData()`.
- Export: `context.Export<ICallControlService>(service)` in `CommunicationPlugin` (nur wenn
  DB-Factory da — wie Purge-Contributor; ohne Factory sauber degradieren: kein Export, kein Crash).
- Tests: Fake-`ICommunicationChannel`/`ICall` + Fake-`ICallLogStore` + Fake-`IBusinessEventBus`;
  belegt Place→Log(Start)+placed, Connected→answered, Terminated→ended, Hangup, Workspace-Scope.

### Slice 1b — Inbound-Beobachtung (in-process) — UMGESETZT
- `IncomingCallObserver` abonniert `registry.ChannelRegistered`/`ChannelUnregistered` (+ Bestand via
  `GetAllRegistrations`) → je Channel `IncomingCall` → `CallControlService.ObserveIncomingAsync` →
  `CallLog.Start(Inbound)` + `call.ringing` + derselbe Lifecycle. **Kein** Auto-Answer, **kein** Routing
  (das ist PBX). Nur beobachten/protokollieren/eventen.
- Gemeinsames Tracking beider Richtungen in `CallControlService.StartTrackingAsync` herausgezogen
  (place vs. observe teilen es). Outcome verfeinert: unbeantwortet + Inbound → `Missed`, Outbound →
  `Failed`. Inbound-Beobachtung fängt Fehler (fire-and-forget aus dem Event); place propagiert weiter.

### Slice 2 — REST-Gesicht + Historie
- `CallsManage`-Permission ergänzen. Routen über `HostAdminApiRouteRegistration`:
  `POST calls` (`CallsManage`) → `PlaceCallAsync`; `GET calls/{id}` (`CallsRead`);
  `POST calls/{id}/hangup` (`CallsManage`); `GET calls?limit=` (`CallsRead`) → `ListRecentAsync`.
- Scope via `SipAccountAdminScope`-Muster (token-bound workspace, sonst `workspaceKey`-Query).

## Decision Log

- **Ein Service, mehrere Adapter** statt getrennter In-/Out-of-process-Pfade — vermeidet
  Doppel-Logik, macht MCP später fast gratis. Alternative (nur REST oder nur Port) verworfen: der
  User will beides (in-process-Plugins **und** externe Agenten/Webhooks/MCP).
- **Channel-neutral (`ICall`), nicht SDK (`IVoipCall`)** — CallLog + `call.*` sind channel-neutrale
  Belange; ein künftiger WebRTC-Channel muss dieselben Events liefern. SDK-Kopplung würde das brechen.
- **Lifecycle im Call-Control-Layer, nicht im `SdkCallAudioRegistrar`** — der Registrar ist der
  SDK-/Audio-Pfad; CallLog/Events gehören an die neutrale `ICall.StateChanged`-Naht, damit auch
  Nicht-SDK-Channels erfasst werden.
- **Inbound = beobachten, nicht routen** — Routing ist PBX-Domäne; Communication bleibt Primitive.
- **Slicing** (1a Outbound → 1b Inbound → 2 REST) hält PRs bissgroß und je für sich testbar.

## Testing

Fast-Tests ohne externe Dienste: Fakes für Channel/Call/Store/Bus. Der reale SDK-Weg ist bereits
durch die Asterisk-Interop-Tests (opt-in) abgedeckt — hier wird die **Steuer-/Log-/Event-Logik**
verifiziert, nicht das Medium.
