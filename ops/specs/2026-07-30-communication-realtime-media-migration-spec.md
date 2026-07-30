# Communication als Real-Time-Media-Basis — Implementierungs-Spec (ADR-016)

**Datum:** 2026-07-30
**Status:** Entwurf zur Review
**Bezug:** ADR-016 (Entscheidung, Variante A), `ops/specs/2026-07-30-videoconference-p1c-sfu-design.md` (der abzulösende VC-Direktweg)

Setzt ADR-016 um: communication wird die einheitliche Real-Time-Media-Basis (einziger SDK-Binder,
hexagonal), die Vertikalen (videoconference, call-center) konsumieren neutrale Verträge und binden nie
die SDK. Dieser Spec schneidet den Umbau in Slices.

---

## 1. Verifizierte Grundlagen (kein neuer Plattform-Mechanismus nötig)

- **Cross-Plugin-Konsum existiert** (`CuratedPluginServiceProvider`, REV2 §9.3 „geteilte Service-Exports"):
  ein Plugin exportiert einen Contract via `context.Export<T>(impl)`; ein anderes löst ihn über
  `context.Services.GetService<T>()` auf — „Host registration wins; otherwise fall back to a cross-plugin
  export". Belegtes Beispiel im Code: **communication liefert `ICommunicationChannelRegistry` an den Dialer**.
  ⇒ `IConferenceService` funktioniert exakt so.
- **`IsAllowed`-Gate:** auflösbar sind nur veröffentlichte Contracts — u. a. **`*.Abstractions`-Pakete**.
  `IConferenceService` lebt daher in **`Callora.Plugin.Communication.Abstractions`** (wie `ICallControlService`,
  `ICommunicationChannelRegistry` schon).
- **Typidentität über die ALC-Grenze:** Konsumenten referenzieren `Callora.Plugin.Communication.Abstractions`
  per ProjectReference (`Private=false ExcludeAssets=runtime`, wie Callora.Core) — der Host liefert die DLL
  zur Laufzeit (eine Typidentität). (Vgl. Memory: callora-production muss diese Referenz führen.)
- **Communication-Struktur passt:** zwei Assemblies (`Callora.Plugin.Communication` + `.Abstractions`);
  Abstractions hat bereits Calls/Voice/Channels/WebRtc-Contracts; die SDK-Bindung liegt gebündelt in
  `src/Infrastructure/Sdk/` (`HeadlessWebRtcClientFactory`, `WebRtcVoiceChannel`, `WebRtcCall`, …). Der
  Umbau ist **additiv + Konsolidierung**, kein Neubau.

---

## 2. Zielstruktur (wo was liegt)

**`Callora.Plugin.Communication.Abstractions` (Konsumenten sehen nur das):**
- `src/Conference/IConferenceService.cs` — der Konferenz-Contract (join/leave, SDP/Candidate-Austausch,
  ausgehende Offer-Notifikation für Renegotiation).
- `src/Conference/` neutrale Signalling-Werttypen: `SessionDescription` (Typ + SDP-String), `IceCandidate`,
  `ConferenceParticipantId`/Ids, `ConferenceOfferEventArgs`. **Keine** SDK-Typen, **keine** rohen MediaFrames
  (die bleiben plugin-intern — der SFU forwardet innerhalb communications).

**`Callora.Plugin.Communication` (intern, sieht als einzige die SDK):**
- `src/Application/RealtimeMedia/IRealtimeMediaProvider.cs` + `IMediaPeer.cs` + neutrale interne Media-Typen
  (`MediaFrame`, `MediaTrackKind`) — der **Provider-Port**.
- `src/Infrastructure/RealtimeMedia/CalloraVoipSdkProvider.cs` — der **Adapter**: hüllt
  `CalloraVoipSdk`-`IWebRtcClient`/`IPeerConnection` in `IMediaPeer`. Konsolidiert die vorhandene
  `src/Infrastructure/Sdk/`-WebRTC-Fläche.
- `src/Application/Conference/ConferenceService.cs` + `ConferenceMediaRouter.cs` — der **SFU über dem Port**
  (portiert aus VCs `SfuRoomMediaRouter`, `IPeerConnection`→`IMediaPeer`).
- `CommunicationPlugin.StartAsync`: `context.Export<IConferenceService>(service)`.

**`Callora.Plugin.VideoConference` (Vertikale, bindet keine SDK mehr):**
- Referenziert `Callora.Plugin.Communication.Abstractions` (ProjectReference).
- Konsumiert `IConferenceService` via `context.Services`. Behält Raum-Domäne, Lobby/Admission, Roster,
  Einladungen, Admin-UI, Vue-Frontend, das WS-Relay.
- **Entfällt:** `Infrastructure/Sdk/*` (WebRtcClientFactory, WebRtcClientOptions), `Infrastructure/Media/Sfu*`
  (wandert nach communication), die direkte `CalloraVoipSdk`-PackageReference.

---

## 3. Neutraler Konferenz-Vertrag (Skizze)

Der Server ist Offerer; Renegotiation bei Topologie-Änderung. Der Vertrag ist **transport-agnostisch** —
er liefert/nimmt SDP+Candidates, die Vertikale relayt sie über ihre eigene authentifizierte WS.

```
interface IConferenceService {
  // Teilnehmer tritt bei → Service legt Server-Peer + Outbound-Tracks an, erzeugt das erste Offer.
  Task<ConferenceParticipant> JoinAsync(conferenceId, participantId, ct);
  // Browser-Answer anwenden (initial ODER Renegotiation).
  Task ApplyAnswerAsync(conferenceId, participantId, SessionDescription answer, ct);
  // Remote-ICE-Candidate anwenden.
  Task AddIceCandidateAsync(conferenceId, participantId, IceCandidate candidate, ct);
  // Teilnehmer verlässt → Peer entsorgen, Forwarding stoppen.
  Task LeaveAsync(conferenceId, participantId, ct);
  // AUSGEHEND an die Vertikale (zum Relay an den Browser): neues Offer (Renegotiation) + lokale Candidates.
  event ...? OfferProduced;      // (participantId, SessionDescription offer)
  event ...? LocalIceCandidate;  // (participantId, IceCandidate)
}
```

Das ersetzt neutral die heutigen VC-Seams `IRoomMediaRouter` (Topologie/Forwarding) **und** die
`RoomSignalingNegotiation` (Offer/Answer/Candidate) — beide wandern hinter `IConferenceService`.
`ConferenceMediaRouter` trägt die schon reviewte SFU-Logik: SendOnly-Outbound-Track je Quelle
(`StreamId=Quelle`), Copy-on-receive-Fan-out, Source-Timestamp 1:1, PLI-an-alle-Upstreams, Renegotiation
bei Join/Leave, Prune/Leave-Semantik.

---

## 4. Slicing (je DEV → Reviewer → Gate; Commits ja, PR erst nach dem Umbau)

- **M1 — Provider-Port + Adapter (intern, kein Consumer-Change):** neutrale interne Media-Typen +
  `IRealtimeMediaProvider`/`IMediaPeer` + `CalloraVoipSdkProvider` (hüllt IWebRtcClient/IPeerConnection).
  *Akzeptanz:* der Adapter erzeugt/verhandelt einen `IMediaPeer`, Tracks add/recv/send, PLI — gegen
  Fakes/echte SDK getestet; bestehende communication-Calls unverändert; Build 0/0.
- **M2 — `IConferenceService` + SFU über dem Port:** Contract in Abstractions; `ConferenceService` +
  `ConferenceMediaRouter` (portierte SFU, `IPeerConnection`→`IMediaPeer`); Export in CommunicationPlugin.
  *Akzeptanz:* Join legt Outbound-Tracks + Renegotiation an, Frames forwarden (Fan-out/Timestamp/Copy),
  Leave räumt auf, PLI-Bridge — portierte Tests gegen `IMediaPeer`-Fakes; Offer/Candidate-Events feuern.
- **M3 — VC-Rückführung:** VC referenziert Communication.Abstractions, konsumiert `IConferenceService`,
  WS-Handler relayt SDP/Candidates (JoinAsync→Offer relayen, Answer→ApplyAnswerAsync, Candidate beидseitig,
  Leave). **Entfernen:** VCs `Infrastructure/Sdk/*` + `Infrastructure/Media/Sfu*` + CalloraVoipSdk-Ref.
  *Akzeptanz:* VC baut ohne CalloraVoipSdk-Ref; Lobby/Roster/Chat/Screenshare/Blur unverändert; Frontend
  (Tiles) unverändert; C#- + Frontend-Gate grün.
- **M4 — Call-Center WebRTC↔SIP-Videoanruf (zweiter Konsument):** ein WebRTC-Leg (über den Provider) an ein
  SIP-Leg (bestehende Call-Control) als **Video** bridgen — validiert die Abstraktion mit einem zweiten
  Konsumenten und den §6-Nutzen aus ADR-016.
  *Akzeptanz:* ein Call-Center-Konsument mintet/relayt einen WebRTC↔SIP-Videoanruf, ohne die SDK zu berühren.

M1→M2→M3 sind die Pflicht-Migration; M4 ist der Beweis der Neutralität durch einen zweiten Konsumenten.

---

## 5. Teststrategie

- **M1/M2:** Unit-Tests gegen `IMediaPeer`-Fakes (das VC-`FakePeerConnection`/`FakeVideoTrack`/`FakeAudioTrack`
  wandert als `FakeMediaPeer` mit — dieselben Recorder: AddedTracks, SentFrames, KeyFrameRequests). Die
  portierten SFU-Tests laufen 1:1 weiter, nur gegen `IMediaPeer` statt `IPeerConnection`.
- **M3:** VCs bestehende Signalling-/Handler-Tests gegen ein `FakeConferenceService` (statt Fake-WebRtcClient);
  Frontend-vitest unverändert.
- **Realer Browser-E2E** bleibt der ehrliche offene Punkt (N-Wege nicht gegen echte Browser validiert) —
  nicht im Merge-Gate.

---

## 6. Risiken / offene Punkte

- **IMediaPeer-Modelltreue:** der Port muss die 4.7.0-Semantik (Multi-Track, mid-call Renegotiation, encodiertes
  Frame-Forwarding, PLI) neutral abbilden, ohne CalloraVoipSdk-Eigenheiten zu leaken. Bei M1 sorgfältig
  schneiden; ein zweiter Adapter beweist die Neutralität erst später (ADR-016 §6).
- **Ausgehende Offer/Candidate-Notifikation:** der Vertrag braucht einen sauberen Kanal (Events/Callback) für
  server-initiierte Offers (Renegotiation) + lokale Candidates. Muss thread-safe an die WS der Vertikale
  relaybar sein (die WS läuft im VC-Prozess/-Handler).
- **Lebenszyklus/Ownership über die Grenze:** wer entsorgt den Server-Peer (Service), wann räumt die Vertikale
  bei Socket-Close (LeaveAsync)? Analog zum heutigen `ParticipantLeftAsync`/Peer-Dispose.
- **SDK-PackageReference:** nach M3 hält **nur** communication die `CalloraVoipSdk`-Referenz; VC verliert sie.
  Deploy/Signing-Bundle von VC schrumpft entsprechend.
- **Bestehende Voice-Fläche:** M1 darf die vorhandenen SIP/WebRTC-Voice-Pfade (WebRtcVoiceChannel etc.) nicht
  brechen — der Adapter konsolidiert, ersetzt aber nicht abrupt.
