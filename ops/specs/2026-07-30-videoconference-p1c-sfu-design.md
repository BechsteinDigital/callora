# Videoconference P1c — SFU-Media-Router (Design)

**Datum:** 2026-07-30
**Status:** Entwurf zur Review
**Repo:** callora-videoconference (Branch-Basis: `feat/sdk-4.7.0-migration`)
**Vorbedingung erfüllt:** CalloraVoipSdk 4.7.0 (Multi-Track + Renegotiation + PLI), Migrations-Branch gemerged/gepusht.

---

## 1. Ziel

Der letzte fehlende Baustein: echtes **Mehrparteien-Video** durch Ersetzen von `NoopRoomMediaRouter`
durch `SfuRoomMediaRouter`. Der Server leitet encodierte Frames zwischen den Teilnehmern eines Raums
weiter (Selective Forwarding Unit) — er decodiert/mischt/transcodiert **nicht**. Browser übernehmen die
gesamte Codec-Arbeit (VP8). Ergebnis: Google-Meet-artige N-Wege-Konferenz.

**Nicht-Ziele P1c:** Simulcast-Layer-Auswahl (bandbreitenadaptiv), aktive Sprecher-Erkennung,
Server-Aufzeichnung, TURN-Server (Connectivity-Plugin), Track-Removal-Renegotiation beim Verlassen
(Tile verschwindet via Roster). Diese sind Follow-ups (§9).

---

## 2. Verifizierte SDK-Grundlagen (aus Deep-Dive 4.7.0)

Diese Fakten sind gegen Implementierung + Integrationstests belegt (siehe `voip/SFU_DOC_FINDINGS_4.7.0.md`):

- **Renegotiation funktioniert end-to-end.** `AddVideoTrack`/`AddAudioTrack` nach Connect + erneutes
  `CreateOffer`/`SetRemoteDescriptionAsync` wenden das Track-Delta auf die LIVE-Session an — kein
  Transport/DTLS/ICE/SRTP-Rebuild (Test `WebRtcRenegotiationPeerToPeerTests`). ⇒ **Join-Anytime**, keine Max-Slots.
- **Empfang:** `IPeerConnection.TrackReceived` → `RemoteTrack` (pro MID); `RemoteTrack.FrameReceived`
  liefert `EncodedFrame` (Payload, RtpTimestamp, IsKeyFrame, Rid, Mid). **Callback synchron auf dem
  Receive-Loop-Thread; Payload nur während des Callbacks gültig → kopieren vor async Fan-out.**
- **Senden:** `IVideoTrack/IAudioTrack.SendFrameAsync(frame, rtpTimestamp)` — der übergebene RTP-Timestamp
  wird 1:1 auf die Wire-Packets gestempelt (A/V-Sync bleibt erhalten). Fire-and-forget, keine Backpressure.
- **PLI:** nicht automatisch. Downstream-PLI feuert `VideoKeyFrameRequested` am Server-Peer; der Router
  muss selbst `RequestVideoKeyFrameAsync(...)` am Upstream-Peer aufrufen. **Impedanz:** das Event trägt
  **keine MID** → der Router weiß nicht, welcher ausgehende Track die PLI auslöste (siehe §9/Findings).
- **Signalling-Ops** (`CreateOffer`/`SetRemoteDescription`/`StartAsync`) sind **single-caller-serialisiert**
  (HARD-C6) — pro Peer nie nebenläufig aufrufen. `SendFrameAsync` ist gegen `DisposeAsync` via Drain-Gate gehärtet.
- **msid/StreamId:** ein ausgehender Track kann via `VideoTrackOptions.StreamId`/`AudioTrackOptions.StreamId`
  einer MediaStream-Id zugeordnet werden → der Browser erkennt am `stream.id` die Quelle.

---

## 3. Topologie

Der Server hält **einen `IPeerConnection` pro Teilnehmer** (schon so — ein Peer pro WS). Der SFU verdrahtet:

```
Teilnehmer P, Peer(P):
  INBOUND  : P's eigene Kamera+Mikro  (TrackReceived auf Peer(P))
  OUTBOUND : je 1 Video- + 1 Audio-Track pro ANDEREM Teilnehmer O
             (Peer(P).AddVideoTrack/AddAudioTrack, StreamId = O.participantId)
```

Frame-Fluss: kommt ein Frame von P herein (Peer(P).TrackReceived→FrameReceived), wird die **kopierte**
Payload an `Peer(O).outboundTrackFor(P)` jedes anderen O gesendet, mit dem Source-RtpTimestamp.

Beispiel Raum {A,B,C}: Peer(A) hat Outbound-Tracks für B und C; A's Inbound-Frames gehen an
Peer(B).outboundFor(A) und Peer(C).outboundFor(A). Symmetrisch für B, C.

Server = **immer Offerer** (schon so: `RoomSignalingNegotiation.StartAsync` erzeugt das Offer, Browser
antwortet). Renegotiation = der Server sendet bei Topologie-Änderung ein *weiteres* Offer; der Browser
antwortet erneut. Kein Glare, weil nur der Server offert.

---

## 4. Lücke: Signalling-Seam für Renegotiation (P1c-1)

Heute sendet `RoomSignalingNegotiation` **genau ein** Offer, und der Answer-Pfad ruft `StartAsync`
(Transport-Start). Für den SFU fehlt:

1. **`RenegotiateAsync(CancellationToken)`** auf `RoomSignalingNegotiation`: `CreateOffer()` →
   `offer`-Frame senden (Kandidaten-Trickle-Gate ist bereits offen). Serialisiert gegen den Answer-Pfad.
2. **`StartAsync`-Guard:** `StartAsync` nur beim **ersten** Answer; Renegotiation-Answers rufen nur
   `SetRemoteDescriptionAsync`. (Flag `_started`.)
3. **Signalling-Gate:** ein `SemaphoreSlim(1,1)` in der Negotiation, das `StartAsync` (initiales Offer),
   `HandleAsync` (Answer/Candidate) und `RenegotiateAsync` serialisiert — HARD-C6 verlangt single-caller.
4. **Router-Trigger:** der Handler übergibt dem Router beim Join einen per-Teilnehmer-Delegaten
   `Func<CancellationToken, Task> requestRenegotiation` (= `negotiation.RenegotiateAsync`). Dazu wird
   `IRoomMediaRouter.ParticipantJoinedAsync` um diesen Parameter erweitert; `NoopRoomMediaRouter` ignoriert ihn.

Der Answer der Renegotiation fließt über den bestehenden Receive-Loop → `negotiation.HandleAsync` →
`SetRemoteDescriptionAsync` auf denselben Peer — der Router muss den Answer **nicht** selbst behandeln.

---

## 5. `SfuRoomMediaRouter` (P1c-2)

Ersetzt `NoopRoomMediaRouter` (gleicher `IRoomMediaRouter`-Seam, + `requestRenegotiation`-Parameter).

### 5.1 Datenstruktur
- `rooms: ConcurrentDictionary<string, Room>`
- `Room`: `participants: Dictionary<string, ParticipantEntry>` unter einem `lock` pro Raum (Topologie-Mutationen sind selten, Frame-Forwarding liest lock-frei über Snapshots).
- `ParticipantEntry`:
  - `IPeerConnection Peer`
  - `Func<CancellationToken,Task> RequestRenegotiation`
  - `outbound: Dictionary<string, OutboundTracks>` (Ziel-Quelle `sourceParticipantId` → `(IVideoTrack, IAudioTrack)`) — die Tracks, über die **dieser** Peer die Medien der Quelle rendert.
  - `remoteTrackHandles` / Event-Subscriptions für Cleanup.

### 5.2 Join (N tritt Raum mit {E…} bei)
Unter dem Raum-`lock`:
1. `ParticipantEntry(N)` anlegen.
2. Für jedes bestehende E:
   - `Peer(E).AddVideoTrack(StreamId=N)` + `AddAudioTrack(StreamId=N)` → `E.outbound[N]`.
   - `Peer(N).AddVideoTrack(StreamId=E)` + `AddAudioTrack(StreamId=E)` → `N.outbound[E]`.
3. `Peer(N).TrackReceived` abonnieren → pro `RemoteTrack` `FrameReceived` → **Copy-on-receive** → an
   `consumers(N)` senden (= alle O mit `O.outbound[N]`), passenden Kind (video/audio), Source-Timestamp.
4. `Peer(N).VideoKeyFrameRequested` abonnieren → Keyframe von **allen** aktuellen Upstreams von N anfordern
   (`Peer(E).RequestVideoKeyFrameAsync()` für alle E) — grob, aber korrekt (Event trägt keine MID, §9).
5. **Renegotiation auslösen:** `N.RequestRenegotiation()` (neue Tracks für alle E) und jedes betroffene
   `E.RequestRenegotiation()` (neuer Track für N). Fire-and-forget mit Fehler-Logging; nicht unter dem lock awaiten.
6. **Initiale Keyframes:** direkt nach Join Keyframe von allen bestehenden E anfordern, damit N schnell
   Intra-Frames für die neuen Tiles bekommt.

### 5.3 Frame-Forwarding
- Handler synchron & nicht-blockierend: `var copy = frame.Payload.ToArray();` dann pro Consumer
  `_ = targetTrack.SendFrameAsync(copy, frame.RtpTimestamp ?? 0, ct)` (fire-and-forget), Fehler pro
  Consumer isoliert (try/catch, weiterforwarden). Video an Video-Track, Audio an Audio-Track.
- Consumer-Set wird pro Frame live aus dem Raum-Snapshot gelesen (neue Teilnehmer erscheinen automatisch).

### 5.4 Leave (N verlässt)
Unter dem Raum-`lock`:
1. `Peer(N).TrackReceived`/`VideoKeyFrameRequested` abmelden; N aus `participants` entfernen.
2. Forwarding N→andere und andere→N stoppt automatisch (N ist raus, `consumers`/`outbound` entfernt).
3. `Peer(N).DisposeAsync()` (Ownership lag beim Router).
4. Die bei E verwaisten `E.outbound[N]`-Tracks bleiben inert (senden nichts mehr); der **Roster-Broadcast**
   des Handlers lässt den Browser N's Tile entfernen. Track-Removal-Renegotiation = Follow-up (§9).

### 5.5 Nebenläufigkeit / Lifecycle
- Topologie-Mutation (Join/Leave) unter Raum-`lock`; Frame-Forwarding lock-frei über Snapshot.
- `SendFrameAsync` ist gegen Peer-Dispose gehärtet (Drain-Gate) → ein Frame, der auf einen gerade
  verlassenden Peer trifft, wirft `ObjectDisposedException`, wird geschluckt.
- `AddVideoTrack/AddAudioTrack` sind thread-safe (lock-frei) und dürfen mid-call aufgerufen werden.

---

## 6. Frontend (P1c-3)

- **Wiederholte Offers:** der Room-Controller muss jedes eingehende `offer`-Frame behandeln
  (`setRemoteDescription(offer)` → `createAnswer` → `answer` senden), nicht nur das erste. Der Browser-
  `RTCPeerConnection` verarbeitet Renegotiation nativ; `ontrack` feuert für jeden neuen Remote-Track.
- **N Remote-Tiles:** `ontrack` → `event.streams[0].id` = Source-`participantId` (Server setzt StreamId).
  Tile pro Remote-MediaStream, DisplayName aus dem Roster (participantId→Name). Video- und Audio-Track
  desselben Streams gehören zu einem Teilnehmer (gleiche StreamId).
- **Tile-Removal:** bei Roster-Update ohne Teilnehmer X → dessen Tile entfernen (deckt Leave ab).
- Bestehende lokale Vorschau/Controls/Chat/Lobby bleiben unverändert.

---

## 7. Slicing & Akzeptanzkriterien

Jede Slice: eigener Branch (stacked), DEV → unabhängiger Reviewer → Findings fixen → Gate (C# 0/0 +
volle Suite; Frontend vue-tsc + vitest + Build) → Push → PR.

- **P1c-1 (Signalling-Seam):** `RenegotiateAsync` + `StartAsync`-Guard + Signalling-Gate;
  `IRoomMediaRouter.ParticipantJoinedAsync` um `requestRenegotiation` erweitert; Noop ignoriert es.
  *Akzeptanz:* zweites Offer geht über die WS raus; Reneg-Answer ruft nicht erneut `StartAsync`; nebenläufiges
  Offer/Answer serialisiert; bestehende Signalling-Tests grün; neuer Test für RenegotiateAsync + Guard.
- **P1c-2 (`SfuRoomMediaRouter`):** Topologie/Forwarding/PLI-Bridge/Leave gemäß §5; in `VideoConferencePlugin`
  Noop→Sfu tauschen.
  *Akzeptanz:* Join fügt Outbound-Tracks + Subscriptions korrekt hinzu und triggert Renegotiation aller
  Betroffenen; Frames werden kopiert und an alle Consumer geforwardet (Timestamp durchgereicht); Leave räumt
  auf und disposed den Peer; Unit-Tests mit `FakePeerConnection` (Multi-Track-Fakes existieren bereits) für
  Join-2/3-Wege, Forwarding-Fan-out, Leave-Cleanup, PLI-Bridge.
- **P1c-3 (Frontend):** wiederholte Offers + N-Tile-Rendering + Track→Teilnehmer-Mapping + Tile-Removal.
  *Akzeptanz:* vitest deckt Mehrfach-Offer-Handling, ontrack→Tile-Mapping via StreamId, Roster-getriebenes
  Tile-Removal ab.

---

## 8. Teststrategie

- **P1c-1/2:** reine Unit-Tests gegen `FakePeerConnection`/`FakeVideoTrack`/`FakeAudioTrack` (bereits im
  Migrations-Branch) — deterministisch, kein echtes WebRTC. Fake erfasst AddedTracks, KeyFrameRequests,
  gesendete Frames pro Track; Tests prüfen Fan-out-Ziele, Timestamp-Durchreichung, Copy (kein Alias),
  Renegotiation-Trigger-Zählung, Leave-Cleanup.
- **P1c-3:** vitest mit injizierten `RTCPeerConnection`-Fakes (Muster wie bestehende media-session-Tests).
- **Manuelle E2E:** 3 echte Browser gegen eine Dev-Instanz — separat, nicht Teil des Merge-Gates
  (SDK-N-Wege-Interop ist noch nicht gegen echte Browser breit validiert, RELEASE_NOTES 4.7.0).

---

## 9. Bewusste Deferrals / Impedanzen

- **PLI ohne MID:** `IPeerConnection.VideoKeyFrameRequested` trägt keine MID → keine gezielte Upstream-PLI;
  P1c fordert Keyframe von allen Upstreams an (grob, korrekt). Gezielt erst, wenn das SDK die MID am Event
  surfaced → SDK-Wunsch (in `SFU_DOC_FINDINGS_4.7.0.md` als B4 vermerkt).
- **Track-Removal beim Leave:** inerte Outbound-Tracks bleiben; Tile verschwindet via Roster. Sauberes
  Track-Removal via Renegotiation später.
- **Simulcast-Layer-Auswahl / bandbreitenadaptives Forwarding** (`RecommendedBitrateChanged`, `frame.Rid`):
  P1c leitet den Einzelstream weiter; Layer-Selektion pro Empfänger ist ein Skalierungs-Follow-up.
- **Aktive-Sprecher / Audio-Selektion:** P1c forwardet alle Audio-Tracks; Server-seitige Sprecherauswahl später.
- **Fan-out-Ordering:** fire-and-forget Sends in Aufrufreihenfolge; falls Reordering auftritt, per-Consumer-Queue als Follow-up.

---

## 10. Decision Log

| # | Entscheidung | Alternative | Grund |
|---|---|---|---|
| D1 | Server ist immer Offerer; Renegotiation = Server re-offert | Perfect-Negotiation mit beidseitigem Offer | Kein Glare, minimal, Browser bleibt reiner Answerer (schon so) |
| D2 | Router-Trigger via `requestRenegotiation`-Delegat in `ParticipantJoinedAsync` | separater `IParticipantRenegotiator` im Registry | Seam bleibt in der Media-Abstraktion, minimale Fläche |
| D3 | `StartAsync` nur beim ersten Answer (Flag) | jedes Answer startet Transport | Zweites StartAsync wäre falsch (Transport läuft schon) |
| D4 | Ein Outbound-Track-Paar pro (Empfänger, Quelle) | ein gemischter Track / SSRC-Multiplex | SDK ist transport-only; ein Track pro Quelle = klare msid-Zuordnung |
| D5 | Track→Teilnehmer-Mapping via `StreamId = sourceParticipantId` | eigenes App-Signalling-Mapping | Nutzt native msid; Browser bekommt Quelle ohne Zusatzframe |
| D6 | PLI bei jeder Downstream-Anforderung an alle Upstreams | gezielt per MID | Event trägt keine MID; throttled → akzeptabel |
| D7 | Leave lässt inerte Tracks, Tile weg via Roster | Track-Removal-Renegotiation sofort | Kleiner, korrekt; Removal-Reneg ist Zusatzkomplexität |
| D8 | Copy-on-receive + fire-and-forget Fan-out | Payload direkt weiterreichen / await | Payload nur im Callback gültig; Handler darf nicht blockieren |
