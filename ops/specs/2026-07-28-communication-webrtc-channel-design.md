# WebRTC-Voice-Channel (Communication) — Design

**Datum:** 2026-07-28
**Status:** akzeptiert (Zuschnitt bestätigt), Umsetzung in Slices
**Kontext:** Communication hat heute nur den SIP-Channel (`SdkVoiceChannel` über `IPhoneLine`). Für
browser-basierte Echtzeit (Softphone in der Admin-Shell, Web-Voice-Agent) und als Fundament für ein
späteres Videokonferenz-Plugin fehlt ein **WebRTC-Channel** — gleichberechtigt neben SIP, über den
WebRTC-Stack des CalloraVoipSdk 4.6.0.

## Understanding
- **Was:** Ein `WebRtcVoiceChannel : IVoiceChannel` + `WebRtcCall : ICall` über die SDK-WebRTC-Peer-
  Primitive, plus ein WebSocket-Signaling-Endpunkt (SDP/ICE) zwischen Browser und Callora.
- **Warum:** channel-neutrale Abstractions (`ICall`/`ICommunicationChannel`) wurden bewusst dafür gebaut.
  Ermöglicht Admin-Shell-Softphone + web-basierten Agenten und ist das geteilte Primitive fürs Konferenz-Plugin.
- **Kernprinzip:** Communication liefert das **WebRTC-Peer-/Signaling-Primitive**; Media-Routing zwischen
  Peers (Raum/SFU) gehört ins spätere Videokonferenz-Plugin, WebRTC↔SIP-Bridging ist ein eigener, vertagter Slice.

### Non-Goals (bewusst NICHT hier)
- **WebRTC↔SIP-Media-Bridge** (Opus↔µ-law-Transcoding, Browser→PSTN) — eigener, vertagter Slice; braucht Codec-Integration.
- **Raum/SFU-Media-Routing** (N Peers) — gehört ins Videokonferenz-Plugin (teilt nur `IWebRtcClient`/`IPeerConnection`).
- **Server-seitiger Media-Consumer** (Voicebot/Agent-Audio über `ICallAudioStream`) — später, wenn ein Consumer da ist.
- Video — das SDK kann es (`EnableVideo`/`WithVideo`), aber v1 fokussiert Audio; Video-Optionen werden durchgereicht, nicht verdrahtet.

## SDK-4.6.0-Fakten (verifiziert)
- Setup: `services.AddCalloraWebRtc(Action<WebRtcOptions>)`; `WebRtcOptions`: AudioCodecs, VideoCodecs, EnableVideo,
  IceServers (`IceServerConfiguration` Host/Port/Type/Transport/User/Pass), DtlsCertificate, LocalEndPoint, SimulcastLayers.
- `IWebRtcClient`: `CreatePeer` → `IPeerConnection`; `Peers` (`IPeerConnectionManager` Active/Count).
- `IPeerConnection` (rohes Transport-Primitive, KEIN Call-Modell):
  - Signaling: `CreateOffer` (→`LocalDescription`), `SetRemoteDescriptionAsync(sdp,ct)`, `AddIceCandidateAsync(cand,ct)`,
    `GatherCandidatesAsync(ct)`, `StartAsync(ct)`; Events `LocalIceCandidateDiscovered`, `ConnectionStateChanged`, `TrackReceived`.
  - Media: `SendAudioAsync(ReadOnlyMemory<byte>,ct)`, `SendVideoFrameAsync(...)`, `AttachMediaTap`, `SendDtmfAsync`.
  - `State`: `PeerConnectionState` { New, Connecting, Connected, Disconnected, Failed, Closed }.
- Media orthogonal zum SIP-Pfad: `RemoteTrack.FrameReceived`→`EncodedFrame` (Payload/RtpTimestamp/IsKeyFrame), rohe
  Encoder-Payload (Opus/VP8), kein Decoder im SDK. NICHT die SIP-`IMediaReceiver`/`IMediaSender`-Taps.

## Architektur
```
Browser (RTCPeerConnection)
   │  WS /ws/communication/webrtc/{token}  (SDP-Offer/Answer + ICE-Candidates, JSON)
   ▼
WebRtcSignalingHandler (IWebSocketHandler)  ── vermittelt Signaling ⇄ IPeerConnection
   │
WebRtcVoiceChannel : IVoiceChannel     (wraps IWebRtcClient; CreatePeer je Session)
   └─ WebRtcCall : ICall               (wraps IPeerConnection; State-Mapping; Hangup=Close)
                                        (Media-Ziel v1: offen/keins — Routing=Konferenz-Plugin, SIP-Bridge=vertagt)
```

## State-Mapping (`PeerConnectionState` → foundation `CallState`)
- New / Connecting → `Connecting`
- Connected → `Connected`
- Disconnected / Failed / Closed → `Terminated` (Failed → `TerminationReason` Category `Failed`)

## Slices
- **S1 — WebRTC-Client-Setup (Foundation):** `AddCalloraWebRtc` in die Communication-Komposition; `WebRtcClientOptions`
  (STUN/TURN/DTLS/Codecs aus Config, analog `VoiceClientOptions`) + Mapping auf `WebRtcOptions`; `IWebRtcClient` verfügbar.
  Kein Channel/Call, nur der Client + Konfig. Tests: Options-Mapping.
- **S2 — Adapter (Core):** `WebRtcCall : ICall` (wraps `IPeerConnection`, State-Mapping, `HangupAsync`=Close, DTMF; Accept/
  Reject werfen `InvalidOperation` — WebRTC kennt kein Ringing-Accept, der Call entsteht per Signaling; `TerminationReason`
  aus `Failed`/Close). `WebRtcVoiceChannel : IVoiceChannel` (Capabilities=[Voice], Health aus Peers/Client). Tests mit Fake-`IPeerConnection`.
- **S3 — Signaling-Transport:** `WebRtcSignalingHandler : IWebSocketHandler` + `WebRtcSignalingContributor :
  IHostWebSocketEndpointContributor` (Route `/ws/communication/webrtc/{token}`, Token-Authorizer wie der Media-Contributor).
  JSON-Protokoll { type: offer|answer|candidate, sdp?, candidate? }; bidirektional Browser⇄`IPeerConnection`. Tests: Handler-Logik.
- **S4 — Provisioning + Registry:** `WebRtcVoiceChannel` in `ICommunicationChannelRegistry` registrieren (per Workspace),
  Export/Wiring im `CommunicationPlugin`. Tests: Provisioning.

## Decision Log
- **Rohes Peer-Primitive → eigener Adapter** (wie SIP `SdkCall`), nicht auf ein SDK-Call-Modell warten (gibt es nicht).
- **Signaling ist App-Job über den vorhandenen WS-Contributor-Seam** — kein neues Transport-Framework; ein zweiter
  Contributor neben dem bestehenden Media-Contributor.
- **Media-Ziel bewusst offen in v1**: 1:1-Human-Call braucht ein Ziel (SIP-Bridge vertagt) oder Peer-Routing (Konferenz-Plugin).
  v1 etabliert Peer + Signaling + Call-Control-Shell; das Routing kommt mit dem jeweiligen Consumer.
- **Kein Video-Wiring in v1** (nur Optionen durchgereicht) — YAGNI bis zum Konferenz-Plugin.

## Offene Punkte (bei Umsetzung/Consumer klären)
- Codec-Strategie (Opus client-seitig; SIP-Bridge bräuchte Transcoding).
- Peer-ID/Token-Binding Browser⇄SDK-Peer (Token wie beim Media-Contributor).
- ICE-Gathering-Deadline (Trickle unbegrenzt vs. Timeout).
- Multi-Workspace-Namespacing (IWebRtcClient singleton, Channels workspace-isoliert).

## Testing
Fast-Tests mit Fakes (`IPeerConnection`/`IWebRtcClient`): Options-Mapping (S1), State-Mapping + Call-Control (S2),
Signaling-Handler-Protokoll (S3), Provisioning/Registry (S4). Realer Browser-Interop analog zum SDK-eigenen
`WebRtcBrowserInteropTests` bleibt opt-in/späterer E2E (kein CI-Default).
