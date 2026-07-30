# ADR-016 — Communication als neutrale Real-Time-Media-Abstraktion (Ports & Adapters)

**Status:** Accepted
**Datum:** 2026-07-30
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ADR-012 — Ein-Core-Extensibility (domänen-neutrale Plattform)
* ADR-013 — Plugin-Trust-Modell (Trusted-in-Process)
* ADR-009 — Pluginverträge und interne Grenzen
* Ergänzt/ordnet ein: das Communication-Call-Control-Modell (ICallControlService, WebRTC-Voice-Channel, credentialed Trunk)

> **Ablösung eines Zwischenschritts (transparent):** Das Videokonferenz-Plugin
> wurde in vier gemergten Branches (`feat/sdk-4.7.0-migration`, `feat/p1c-1/2/3`)
> zunächst so gebaut, dass es die **CalloraVoipSdk direkt** bindet (eigener
> `IWebRtcClient`, `IPeerConnection`, `SfuRoomMediaRouter`). Das war ein
> funktionierender, review-ter Zwischenschritt (SFU end-to-end, 4.7.0-Multi-Track/
> Renegotiation/PLI, Frontend-Tiles). Dieses ADR **supersedet** genau diese direkte
> SDK-Bindung im VC-Plugin: die Media-/WebRTC-Fläche zieht hinter das
> Communication-Plugin. Die gebaute Forwarding-/Signalling-/Frontend-Logik ist
> **nicht verworfen**, sondern wird migriert (siehe §7).

---

## 1. Kontext

Callora ist **API-First** und **domänen-neutral** („eigenes Shopware/Symfony für .NET",
ADR-012). System-Plugins sollen Ökosystem-Entwicklern **Fach-Fähigkeiten so einfach wie
möglich** bereitstellen, ohne dass diese die Infrastruktur selbst verdrahten müssen.

Das **communication-Plugin** ist genau ein solches System-Plugin: es kapselt die
Real-Time-Kommunikation (Voice, WebRTC, SIP). Der **Zweck** ist, dass andere Plugins —
die Vertikalen (Call-Center, Dialer, PBX, CRM, AI-Agent, Videokonferenz) — Telefonie/
Media **nicht selbst** implementieren und **nicht direkt** gegen eine WebRTC-/VoIP-SDK
programmieren müssen. Wenn jede Vertikale die SDK selbst bindet, entstehen N Kopien des
WebRTC-Setups, N ICE-/TURN-Konfigurationen und N Stellen, an denen ein CVE zu patchen ist.

**Auslöser dieser Entscheidung** waren zwei konkrete Fälle:

* **Call-Center-Videoanruf:** Ein Call-Center-Agent führt einen **WebRTC↔SIP-Videoanruf**.
  Das Call-Center-Plugin soll dafür **nicht** die SDK ansprechen, sondern communication —
  „verbinde diesen WebRTC-Agenten mit SIP-Nummer X als Videoanruf". communication bindet
  die SDK, das Call-Center-Plugin bekommt die Fähigkeit über den Vertrag.
* **Videokonferenz-SFU:** N-Wege-Mehrparteien-Video. Der Zwischenschritt band die SDK
  direkt — das widerspricht dem Zweck des System-Plugins.

**Zusätzliche Randbedingung (erzwungen):** communication muss **anbieter-neutral** sein.
Die CalloraVoipSdk ist **eine** Implementierung dahinter; es muss möglich sein, eine **andere
WebRTC-/VoIP-SDK, die in diesen Bereich passt**, anzuschließen, ohne die konsumierende
Fläche zu ändern. „Jede andere SDK" meint dabei realistisch **jede WebRTC-förmige SDK dieser
Domäne** (Offer/Answer, encodiertes Frame-Forwarding, Trunk/SIP) — kein Universaladapter für
beliebige Protokolle.

---

## 2. Entscheidung

Das **communication-Plugin ist die einzige Komponente, die eine konkrete Real-Time-Media-SDK
bindet.** Es ist **hexagonal (Ports & Adapters)** aufgebaut, mit **neutralen Verträgen in beide
Richtungen**:

1. **Nach oben — Konsumenten-Verträge** (was Vertikalen konsumieren; keine SDK-Typen):
   * `ICallControlService` — **Calls** (2-Party: WebRTC↔SIP, Browser↔Trunk), Audio **und** Video.
   * `IConferenceService` (neu) — **Conferences** (N-Party-Räume mit serverseitigem SFU-Forwarding).
2. **Nach unten — Provider-Port** (was eine SDK adaptiert): ein neutraler
   `IRealtimeMediaProvider`, der serverseitige **neutrale** Media-Peers (`IMediaPeer`) und
   SIP-Legs erzeugt/verhandelt. **`CalloraVoipSdkProvider` ist der erste Adapter** — per
   Konfiguration/DI gegen einen anderen Adapter austauschbar.
3. **Neutrale Werttypen** an beiden Grenzen (`MediaFrame`, `MediaTrackKind`, `SessionDescription`
   [SDP als String], `IceCandidate`, `IceConfig`) — **kein `CalloraVoipSdk.*` leakt** nach oben,
   und ein Adapter muss nur das WebRTC-förmige Port-Modell erfüllen.

Die **SFU-Forwarding-Logik liegt ÜBER dem Provider-Port** (in der `IConferenceService`-
Implementierung), gebaut auf `IMediaPeer` — damit ist sie **SDK-neutral** und funktioniert mit
jedem Adapter. communication ist **transport-agnostisch fürs Browser-Signalling**: es liefert
Offers, nimmt Answers, emittiert Candidates — die **Vertikale relayt** diese über ihre eigene,
authentifizierte Transportschicht (WebSocket).

---

## 3. Schichtung

```
┌───────────────────────────────────────────────────────────────────────────┐
│ Vertikalen (eigene Plugins): Call-Center · Videoconference · Dialer · PBX · │
│ AI-Agent — Fachlogik, Auth, UI, Policy. Binden KEINE SDK.                    │
└───────────────▲───────────────────────────────────▲───────────────────────┘
   konsumiert   │ ICallControlService                │ IConferenceService
                │ (Calls, WebRTC↔SIP, A/V)           │ (N-Wege-SFU-Räume)
┌───────────────┴───────────────────────────────────┴───────────────────────┐
│ communication-Plugin (System-Plugin)                                        │
│  • Konsumenten-Services (oben) — neutrale Fläche                            │
│  • SFU-Forwarding / Call-Orchestrierung — ÜBER dem Provider-Port            │
│  • IRealtimeMediaProvider (Port) + neutrale Werttypen                       │
│  • CalloraVoipSdkProvider (Adapter) ── bindet als EINZIGE die SDK           │
└───────────────────────────────────────▲───────────────────────────────────┘
                                         │ adaptiert
┌────────────────────────────────────────┴──────────────────────────────────┐
│ CalloraVoipSdk (austauschbar) · später: alt. WebRTC/VoIP-SDK-Adapter        │
└────────────────────────────────────────────────────────────────────────────┘
```

* **Nur** der `CalloraVoipSdkProvider` in communication kennt `CalloraVoipSdk`.
* Weder Konsumenten-Verträge noch Vertikalen sehen SDK-Typen.
* Das **Connectivity-Plugin** (STUN/TURN-Server, eigenständig/kommerziell) bleibt getrennt und
  liefert die ICE-Config als Capability an communications Adapter (ADR-übergreifend) — es ist
  **nicht** Teil dieser Abstraktion.

---

## 3a. Plugin-Zuschnitt: communication rescopen, kein neues Substrat-Plugin

Erwogen wurde, das SDK-bindende Media-Substrat (Provider-Port + Adapter + neutrale Primitive) in
ein **eigenes** System-Plugin unter communication zu ziehen (communication bliebe eng Telefonie,
Substrat separat). **Entschieden: nein — communication wird rescopet** zur einheitlichen
**Real-Time-Media-Basis** (Calls + Conferences), à la **Azure Communication Services / Twilio**
(die unter „Communication" Calling, Video, Conferencing, SMS vereinen). Begründung:

* Die geforderte **SDK-Neutralität liefert der interne Provider-Port** (§4) — in beiden Varianten
  identisch. Ein separates Plugin kauft **keine** zusätzliche Austauschbarkeit, nur zusätzliche Struktur.
* **Weniger versionierte Grenzen:** jede Plugin-Grenze ist ein dauerhaft zu pflegender Vertrag
  (BC/ApiCompat). Ein Substrat-Plugin fügt eine Substrat↔communication-Grenze hinzu, die dieser
  Zuschnitt nicht hat.
* **Mostly additiv statt Umzug:** communication bindet die SDK und macht Call-Control bereits — dieser
  Zuschnitt ergänzt `IConferenceService` + den Port; ein Substrat-Plugin müsste die Bindung erst
  herauslösen (mehr Churn + ein neues Deployment/Signing/Manifest-Artefakt).
* **Nicht foreclosing:** Port + neutrale Primitive sind eine saubere Naht — ein späteres Herauslösen
  des Substrats in ein eigenes Plugin bleibt ein mechanischer Schnitt. Regret-arme Wahl.

**Identitäts-Rescope (bewusst):** communication war als „Voice-Primitive" skizziert; es wird zur
**einheitlichen Real-Time-Communication-Basis** (Voice + Video + Conferencing über die neutrale
Fläche). Videokonferenz und Call-Center bleiben **eigene Vertikalen**, die konsumieren — die
Vertikalen-Trennung ändert sich nicht, nur communications Reichweite.

**Wann ein eigenes Substrat-Plugin (B) doch gewinnt:** wenn „communication" in der Produkttaxonomie
strikt Telefonie bleiben soll, oder wenn Nicht-Kommunikations-Plugins rohes Media bräuchten — beides
aktuell nicht der Fall.

---

## 4. Provider-Port (Abwärts-Neutralität)

`IRealtimeMediaProvider` ist die **einzige** Naht zur SDK. Er muss ausreichen, um darauf sowohl
Calls als auch die SFU zu bauen, und dabei **minimal + neutral** bleiben. Erforderliche Fähigkeiten
(in neutralen Typen):

* **Server-Peer erzeugen/entsorgen** → `IMediaPeer` (neutrales Pendant zu einem WebRTC-Peer).
* **Verhandeln:** Offer erzeugen · Answer/Remote-Description anwenden · Renegotiation
  (mid-call Track-Delta) · lokale ICE-Candidates emittieren · Remote-Candidate anwenden.
* **Tracks:** Video-/Audio-Track hinzufügen (Richtung, StreamId) · (später) entfernen.
* **Media:** encodierten Frame **empfangen** (pro Remote-Track, mit RTP-Timestamp/Keyframe-Flag/
  StreamId) · encodierten Frame **senden** (pro Track, Timestamp durchgereicht) · **Keyframe
  anfordern** (PLI).
* **SIP-Leg** (für Calls): Trunk-Registrierung/-Call — hinter derselben Provider-Fläche oder einer
  parallelen SIP-Port-Erweiterung.

**Bewusst kodierte Annahmen** (die Domänen-Grenze): Offer/Answer-Modell, encodiertes
Frame-Forwarding (Transport-only, App/Consumer besitzt den Codec nicht — der Browser tut es),
per-Track-Multiplex. Eine SDK, die diese Form erfüllt, ist adaptierbar; ein völlig andersartiges
Media-Modell ist es nicht (und soll es laut Scope nicht sein).

---

## 5. Konsumenten-Verträge (Aufwärts-Neutralität)

* **`ICallControlService` (Calls):** ein Call ist 2-Party (ein WebRTC-Leg ↔ ein SIP-Leg, oder
  Browser↔Trunk), **Audio und Video**. Deckt den Call-Center-Videoanruf: die Vertikale gibt
  „WebRTC-Agent ↔ SIP-Ziel, mit Video", bekommt Offers/Answers/Candidates neutral zurück und relayt
  sie an den Browser; das SIP-Leg macht communication.
* **`IConferenceService` (Conferences):** ein Raum mit N Teilnehmern; communication hält je einen
  `IMediaPeer` pro Teilnehmer und **forwardet** encodierte Frames zwischen ihnen (SFU: ein
  SendOnly-Track je anderem Teilnehmer, `StreamId = Quell-Teilnehmer`; Copy-on-receive;
  Source-Timestamp 1:1; PLI-Bridge). Die Vertikale (Videoconference) liefert Teilnehmer-Beitritt/-
  Austritt und relayt SDP/Candidates über ihre eigene WS; **Policy, Lobby, Roster, Einladungen, UI
  bleiben in der Vertikale.**

Beide Services sind **transport-agnostisch**: sie produzieren/konsumieren SDP + Candidates als
neutrale Werte. Die Vertikale besitzt weiterhin die authentifizierte Browser-Verbindung.

---

## 6. Konsequenzen

**Positiv:**

* Ökosystem-Entwickler binden **nie** eine Media-SDK; sie konsumieren zwei klare Verträge.
* **Ein** Ort für SDK-Version, ICE/TURN, Media-Härtung, Security, Capability-Gating.
* **SDK-Austauschbarkeit**: ein zweiter Adapter tauscht die SDK, ohne Konsumenten zu berühren.
* Konsistenz: alle Real-Time-Media in Callora läuft durch eine auditierte, gehärtete Schicht.

**Kosten / Trade-offs:**

* communication trägt die Media-Komplexität (SFU + Call-Orchestrierung) — mehr Fläche zu designen/
  pflegen. Bewusst, im Sinne des System-Plugin-Zwecks.
* Die Neutralität ist **domänen-begrenzt** (WebRTC/SIP-förmig, §4) — kein Universaladapter.
* **Migration** des VC-Zwischenschritts (§7) statt „auf der grünen Wiese".
* Ein zweiter Adapter ist erst dann wirklich validiert, wenn er einmal geschrieben wurde
  (bis dahin ist der Port „neutral by design", nicht „neutral by proof").

---

## 7. Migration des Videokonferenz-Zwischenschritts

Die vier gemergten Branches werden **nicht rückgängig gemacht**, sondern verortet neu:

* **`SfuRoomMediaRouter` + Renegotiation-Seam** (P1c-1/P1c-2) → wandern nach communication als
  Innenleben von `IConferenceService`, umgeschrieben von `IPeerConnection` auf `IMediaPeer`
  (neutral). Die Forwarding-Topologie/PLI-Bridge/Copy-Semantik bleibt inhaltlich.
* **WebRTC-Client-Konstruktion + ICE-Config** (`VideoConferenceWebRtcClientFactory`,
  `WebRtcClientOptions`, `DefaultBrowserIceConfigProvider`) → werden Teil des
  `CalloraVoipSdkProvider`-Adapters bzw. der ICE-Config-Capability.
* **VC-Plugin** behält Raum-Domäne, Lobby/Admission, Roster, Einladungen, Admin-UI, Vue-Surface-UI,
  das WS-Signalling-Relay — und konsumiert `IConferenceService` statt `new WebRtcClient(...)`.
* **Frontend** (P1c-3, Tiles/ontrack) bleibt in VC unverändert (browser-seitig, unabhängig davon,
  wer serverseitig die SDK bindet).

Ein separater Implementierungs-Spec (`ops/specs/`) schneidet die Migration in Slices
(Provider-Port + Adapter · IConferenceService/SFU-Umzug · VC-Rückführung · Call-Center-Videoanruf
als zweiter Konsument) im etablierten DEV→Reviewer→Gate-Takt.

---

## 8. Nicht-Ziele / Abgrenzung

* **Kein** Universal-Media-Adapter für beliebige Protokolle — WebRTC/SIP-Domäne (§4).
* **Kein** Umzug von Auth/Lobby/Roster/UI aus den Vertikalen — die bleiben Fachlogik der Vertikale.
* **Connectivity-Plugin** (TURN/STUN-Server) bleibt eigenständig; es speist die ICE-Config, ist aber
  nicht Teil dieser Abstraktion.
* **Kein** Zwang, bestehende reine-Voice-Konsumenten sofort umzustellen; `ICallControlService`
  behält seine Form, gewinnt nur Video + die saubere Provider-Trennung.
