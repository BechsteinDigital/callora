# Videokonferenz-Plugin (Google-Meet-Klon) — Design

**Datum:** 2026-07-28
**Status:** akzeptiert (Design bestätigt), Umsetzung in Slices — **überarbeitet 2026-07-28** (workspace-gebundenes
Zugangsmodell mit Gast-Lobby + Host-Admission; UI durchgängig Vue/Surface statt SSR/C#-HTML).
**Kontext:** Erster echter WebRTC-Multi-Party-Konsument der Plattform und das Vorzeige-Plugin für die
Extensibility (Surface-Views, Admin-Slot, Extension Points). Baut auf dem WebRTC-Voice-Channel-Primitive
(Communication) NICHT auf — es ist eine eigenständige Vertikale, die das CalloraVoipSdk-WebRTC direkt nutzt.

## Understanding
- **Was:** Ein workspace-gebundenes `videoconference`-Plugin, das browser-basierte Videokonferenzen über einen
  **serverseitigen SFU** (Selective Forwarding Unit) liefert: Einladungslink → Name-Formular → Raum mit
  Video/Audio, Chat, Screensharing und (client-seitigem) Hintergrundblur.
- **Warum:** Demonstriert die Plattform end-to-end (Surface-Vue für die Raum-App, Admin-Vue für die Verwaltung,
  Host-WS-Seam fürs Signaling, Public-HTTP-Seam für Gäste, Plugin-Lifecycle) und ist der Beweis, dass ein Dritter
  die Plattform *von außen* erweitern kann. Zugleich das erste vorzeigbare Produkt.
- **Kernprinzip SFU:** **SFU ist der moderne Standard** für Multi-Party (Mesh skaliert nur bis ~4-6, MCU ist alt/teuer).
  Der SFU leitet encoded Frames weiter (kein Decode/Re-Encode). Die generischen **Multi-Track-Peer-Primitive**
  (Transceiver/Renegotiation/PLI) gehören ins SDK (Release 4.7.0, paralleler Track); der **SFU selbst**
  (Raum, Forwarding-Policy, Teilnehmer) lebt im Plugin — exakt die SIPSorcery-Trennung (Lib = Multi-Track-Peer,
  App = SFU).
- **Kernprinzip Zugang (Meet/Teams/Zoom-Modell):** Alles ist **workspace-gebunden**. Zwei Wege in *denselben* Raum,
  in *dieselbe* Vue-Raum-App:
  1. **Workspace-Nutzer** (authentifiziert): erreicht den Raum über die **workspace-gebundene Surface**. Je nach
     Bypass-Policy direkt als **Host** oder **Teilnehmer**.
  2. **Gäste** (nicht eingeloggt, aber *Gast dieses Workspaces*): erreichen den Raum über einen **Einladungslink**,
     der über die normale Workspace-Auflösung (Host/Pfad) denselben Workspace auflöst → **anonym erreichbare
     „join"-Surface** → Name-Formular → **Lobby (knock)** → **Host lässt ein (Admission)** → Rolle **Gast**.
  Der Unterschied zwischen beiden ist **Identität + Lobby-Gate**, nicht die App: beide laufen in derselben
  Surface-Vue-Raum-App, mit dem Theme und dem Raum *dieses* Workspaces.

### Non-Goals (bewusst NICHT hier)
- **Mesh / P2P** und **Server-relayed Mesh** — verworfen zugunsten des echten SFU.
- **Multi-Track-Peer + PLI/FIR-Senden** — SDK-Sache (Release 4.7.0), NICHT im Plugin. Das Plugin konsumiert
  die Fähigkeit über den Media-Seam.
- **Server-seitiges Compositing/Transcoding (MCU)** — das SDK ist transport-only, kein Decoder/Encoder.
- **Externer SFU** (LiveKit/mediasoup/Janus) — widerspricht der „auf Basis CalloraVoipSdk"-Strategie.
- **C#-gebautes HTML** — der frühere `HtmlRenderer`-Ansatz ist **verworfen**. Gast- wie Mitglieds-UI ist Vue/Surface.
- **Workspace-unabhängige Gast-App** — der Gast ist workspace-gebunden (nur anonym), keine losgelöste öffentliche App.
- **Recording/Moderation/Breakout-Räume** — spätere Ausbaustufen, nachdem der SFU-Kern steht.

## Abhängigkeiten & Annahmen
- **SDK 4.7.0 (paralleler Track, in Arbeit):** liefert Multi-Track-Peers (`AddTrack`/Transceiver), SDP-Renegotiation
  und ausgehendes PLI/FIR (Keyframe-Request). Bis dahin ist der **Media-Seam gestubbt** — alles andere ist baubar.
- **Gemergt auf main:** WebRTC-Voice-Channel (Communication) + SIP-Härtung. Der WebRTC-Signaling-**Härtungscode**
  (Answer-Deadline, Trickle-Gate, TOCTOU-Peer-Claim) ist das Referenzmuster; für v1 im Plugin dupliziert
  (nicht vorzeitig in eine geteilte Lib gezogen).
- **Plattform-Seams (bereits vorhanden):**
  - **`IHostWebSocketEndpointContributor`** — Raum-Signaling **und Lobby/Admission**.
  - **`IHostAdminApiExtensionContributor`** — authentifizierte Admin-API-Routen **mit RBAC** (`PermissionKeys`,
    `RequiredPermission` je Route) **und Navigation** (`NavigationItems`). Trägt die Raum-Verwaltung; voll verdrahtet.
  - **`IHostPublicHttpEndpointContributor`** — anonyme **JSON**-Endpunkte (Einladung prüfen, Name → Pending-Session).
    *PR offen — Beibehaltung ist eine offene User-Entscheidung.* Der Einladungstoken trägt den Workspace-/Raum-Scope,
    sodass der anonyme Datenweg trotzdem workspace-gebunden bleibt.
  - **Anonyme Plugin-Asset-Auslieferung** (`/plugin-assets/{pluginId}/app/{surface}/`), Manifest, `ui-chain`,
    Theme-Tokens — die Vue-Bundles; Surface-Views registrieren sich über `window.calloraSurface`, Vue external.
- **Surface-Modell (bereits vorhanden — das ist der Gast-Weg):** Ein Workspace hat **N `WorkspaceSurface`** auf
  geteilten Daten, **jede mit eigener URL** (`PublicHost`/`PublicPathPrefix`), eigenem **`AccessMode`**
  (`Public`/`Authenticated`/**`Mixed`**), eigenem **`TemplatePluginId`** und Theme (ADR-014 §5/§6.1). **`Mixed`** =
  *öffentliche UND geschützte Routen auf derselben Surface* — genau das Meet-Modell: die Raum-Surface trägt den
  öffentlichen Gast-Join **und** den geschützten Mitglieder-Bereich unter *einer* URL. **Kein neuer Plattform-Seam
  nötig** — aber die per-Surface-Auflösung (Route → konkrete `WorkspaceSurface`) + das per-Surface-`AccessMode`-Gate
  im Render-/Lade-Pfad sind noch nicht verdrahtet (im Code als „later phase" markiert; siehe die
  „Plattform-Verdrahtung"-Zeile unten, C-Surface). Das ist ein **kleiner echter Workstream**, kein Seam-Neubau.
- **Base-Template + Erweiterung pro Surface (bereits vorhanden):** `SurfaceShell.SpaRoot` ist das neutrale
  **Grundgerüst** (ein Mount-Punkt, keine eigene UI); pro Surface erweitert/ersetzt ein Template-Plugin
  (`TemplatePluginId`) es, Includes über `@bundleId/...` (`ISurfaceTemplateBundleProvider`). Das ist der
  „Blöcke/Extending"-Mechanismus, den das Plugin vorführt — die Raum-App ist eine Surface-Erweiterung des Base-Templates.
- **SSR (Nunjucks) + Vue (dynamisch) — Hybrid:** Die Surface-Shell wird **serverseitig via Nunjucks** gerendert
  (`/surface/render` → `NunjucksSurfaceRenderer`), **Vue** übernimmt die **dynamischen Inhalte** (Live-Raum,
  Video-Grid, Chat, Lobby). Das Plugin steuert die SSR-Seite als **Template-Bundle** bei: `index.njk` (Entry, mit
  `extends`/`block`/`include`, cross-bundle `@id/path`) unter `plugin-assets/<id>/views/workspace`; die Plattform
  rendert es mit **Theme-Tokens** (`{{ tokens.<key> }}` → `--cal-*`). Das Plugin ruft `ISurfaceRenderer` **nicht
  selbst** auf (es *liefert* Templates) — die Nicht-Erreichbarkeit von `Callora.Surface.Rendering` blockiert diesen
  Weg also nicht. Die SSR-Shell bootet die Surface-Runtime (`/surface-app/surface.js`), die die dynamischen
  Vue-Views (`window.calloraSurface`) einhängt.
- **Plattform-Verdrahtung (kleiner, echter Workstream — im Code als „later phase" markiert):** `/surface/render`
  löst heute nur **Workspace → Default-Surface** auf und gated am **workspace-weiten** `SurfaceAccessPolicy`. Die
  **per-Surface-Auflösung** (Route → konkrete `WorkspaceSurface`) **+ per-Surface `AccessMode`-Gate** (Public/Mixed)
  fehlt noch — genau das braucht die `Mixed`-Raum-Surface (öffentlicher Gast-Join + geschützter Mitgliederbereich
  unter einer URL). Eigener Branch→PR in callora (C-Surface); **modellierte Phase fertigstellen, kein neuer Seam.**
- **Admin-Shell** (Vue-3-SPA) mit Extension-Slot-Mechanismus (`extension.page.<id>`, IIFE-Bundle, Vue external →
  `window.CalloraAdmin.vue`) verfügbar — siehe Plugin-Admin-UI-Bundle-Muster.

## Architektur

### Verortung & Schichten
Eigenständiges, **workspace-gebundenes** Plugin `custom/plugins/VideoConference/` (Drittanbieter-Tier —
Vorzeige-Extensibility, KEIN System-Plugin, keine Communication-Kopplung). DDD+Feature-sortiert, ein Typ pro Datei:
- **Domain** — `Room`, `Participant`, `ParticipantRole` (Host/Teilnehmer/Gast), `Invitation` (workspace-/raum-scoped),
  `RoomToken`, `LobbyEntry`/Admission-Zustände, `RoomAccessPolicy` (Lobby-Bypass), Lebenszyklus.
- **Application** — `IRoomService` (Raum anlegen/beenden), `IInvitationService` (Link mint/validieren),
  `IRoomSessionMinter` (Session mit Admission-Zustand), `ILobbyService` (knock/admit/reject/kick),
  `IRoomChatService`, `IRoomMediaRouter` (der Media-Seam), Signaling-Choreografie.
- **Infrastructure** — EF-Persistenz (eigener DbContext, wie Communication), `IWebRtcClient`-Setup
  (eigene SDK-WebRtc-Instanz), die konkrete `SfuRoomMediaRouter`-Impl (wartet auf SDK 4.7.0).
- **Api** — WS-Contributor (Raum-Signaling + Lobby), Admin-API-Contributor (Verwaltung), Public-HTTP-Contributor
  (Gast-JSON), Surface-View-Bundle(s) (`src/Resources/public/{surface}` — Mitglieds- und Gast-Raum-App),
  Admin-UI-Bundle (`src/Resources/public/admin`).

### Zugang, Rollen & Lobby
- **Rollen** (`ParticipantRole`):
  - **Host** — hat den Raum angelegt bzw. Workspace-Nutzer mit Host-Recht; steuert die Lobby (admit/reject/kick).
  - **Teilnehmer** — authentifizierter Workspace-Nutzer, voller Medien-Teilnehmer.
  - **Gast** — zugelassener anonymer Externer (über Einladungslink + Admission).
- **Lobby / Admission:** Ein Teilnahmeversuch mündet in eine **Session mit Admission-Zustand**:
  - `Admitted` — Workspace-Nutzer, der laut Bypass-Policy direkt eintreten darf (Host/Teilnehmer).
  - `Pending` — Gast (bzw. jeder, den die Policy zum Anklopfen zwingt): landet in der **Lobby**, ein **Host**
    bekommt den Knock (Broadcast an Host-Sockets) und **lässt ein oder ab**. Erst nach `admit` wird der Peer
    erzeugt und der Raum betreten; `reject`/Timeout → Verbindung geschlossen.
- **Bypass-Policy pro Raum** (`RoomAccessPolicy`, v1 zwei Werte):
  - `WorkspaceTrusted` (Default): authentifizierte Workspace-Mitglieder treten direkt ein; **Gäste klopfen immer an**.
  - `LockedToHost`: alle außer dem Host klopfen an (auch Workspace-Mitglieder).
- **Lobby-Control lebt in der Host-Raum-View**, nicht im Admin-Slot: der Host ist selbst im Raum und sieht die
  Klopfenden dort → kein Admin-seitiger Push-Kanal nötig. Der Admin-Slot bleibt für **Verwaltung** (anlegen/auflisten/
  beenden, Policy setzen), nicht für Live-Admission.

### UI (SSR-Nunjucks + Vue-dynamisch, kein C#-HTML)
- **Admin-Slot (Vue-IIFE, `extension.page.videoconference`)** — Raum-Verwaltung über den **Admin-API-Seam**
  (`IHostAdminApiExtensionContributor`): Räume anlegen/auflisten/beenden, Einladungslinks, Bypass-Policy.
  Authentifiziert, RBAC-permission-gated, mit eigenem Navigationseintrag.
- **Surface-SSR-Shell (Nunjucks)** — das Plugin liefert `index.njk` (Entry) + Includes als Template-Bundle
  (`views/workspace`), das die **Base-Shell erweitert** (`extends`/`block`); die Plattform rendert serverseitig mit
  Theme-Tokens (`{{ tokens.<key> }}`). Trägt die statischen/ersten Teile: Raum-Rahmen, Einladungs-Landing,
  Name-Formular-Gerüst — SEO- und First-Paint-fähig.
- **Surface-Vue-View (dynamisch, `window.calloraSurface`, Vue external)** — die dynamische Raum-App (Video-Grid, Chat,
  Lobby-Wartezustand, für den Host die **Admission-Kontrolle**, Screenshare-/Blur-Toggle). Wird von der SSR-Shell über
  die Surface-Runtime (`/surface-app/surface.js`) in die gerenderte Seite eingehängt. **SSR = Rahmen/statisch,
  Vue = dynamisch/live.**
  - **Mitglieder** und **Gäste** laufen auf **derselben `WorkspaceSurface`** (`AccessMode = Mixed`, eigene URL,
    `TemplatePluginId` = die Raum-App als Erweiterung der Base-Shell): Mitglieder über die geschützten Routen
    (authentifiziert), Gäste über die öffentlichen Routen (anonym) — Theme = Surface-Theme. Ein Codebase, zwei
    Einstiegskontexte (authentifiziert vs. anonym+Lobby), *eine* URL/Surface desselben Workspaces.

### WebRTC-Andockung
Das Plugin nutzt **CalloraVoipSdk.WebRtc direkt** (eigener `IWebRtcClient`), nicht den Communication-Voice-Channel.
Der SFU braucht `IPeerConnection`-Level-Zugriff (`AttachMediaTap` + `SendVideoFrameAsync`), den der `ICall`-Level
des Voice-Channels bewusst verbirgt. Callora ist **Offerer** (wie beim Voice-Channel): je Teilnehmer erzeugt der
Server einen Peer.

### Raum-Signaling (mit Lobby-Gate)
Eigener WS-Endpunkt `/ws/videoconference/room/{connectToken}` über den Host-WS-Contributor-Seam. Ablauf pro Teilnehmer:
1. Browser (Mitglied über Surface / Gast über Name-Formular) löst eine **Session** ein: `IRoomSessionMinter` mint
   einen `connectToken` mit **Admission-Zustand** (`Admitted` | `Pending`) + Rolle + Workspace-/Raum-Scope.
2. Browser öffnet den Signaling-WS mit dem Token → Authorizer konsumiert es (single-use/TTL).
3. **Lobby-Gate:** Ist die Session `Pending`, hält der Handler die Verbindung im **Lobby-Zustand** (noch **kein**
   Peer): Knock an die Host-Sockets broadcasten, auf `admit`/`reject`/Timeout warten. Bei `admit` → weiter zu 4,
   sonst sauberes Schließen. Ist die Session `Admitted`, direkt zu 4.
4. Server erzeugt **einen serverseitigen Peer**, tauscht Offer/Answer/ICE (gehärtetes Muster).
5. Teilnehmer wird in die **Raum-Registry** aufgenommen; Join/Leave + Teilnehmerliste (mit Rollen) an alle gebroadcastet.
6. Der Peer wird dem `IRoomMediaRouter` übergeben — **hier** hängt das Forwarding (siehe Media-Seam).

```
Browser (RTCPeerConnection, N Remote-Tracks)
   │  WS /ws/videoconference/room/{token}   (SDP/ICE + Raum-Choreografie + Chat + Lobby, JSON)
   ▼
RoomSignalingHandler ── Lobby-Gate (Pending→knock→admit) ── Peer-Lebenszyklus + Raum-Mitgliedschaft
   │                                                              │
   │                                                              ▼
   │                                                       IRoomMediaRouter  ◄── Media-Seam (Stub jetzt, SFU nach 4.7.0)
   ▼
Room (Registry: N Participants mit Rolle, je 1 serverseitiger IPeerConnection)
```

## Der Media-Seam (die zentrale Entkopplung)
`IRoomMediaRouter` (Application) trennt „Raum + Signaling + Lobby + Peer-Lebenszyklus" (jetzt baubar) von
„Multi-Track-Frame-Forwarding" (wartet auf SDK 4.7.0):

```
interface IRoomMediaRouter
    ParticipantJoined(roomId, participantId, IPeerConnection peer)   // Server-Peer verdrahten
    ParticipantLeft(roomId, participantId)                            // Taps/Tracks lösen
    TrackPublished(roomId, participantId, TrackKind)                  // z.B. Screenshare an
    TrackUnpublished(roomId, participantId, TrackKind)
```

- **`NoopRoomMediaRouter` (jetzt):** akzeptiert Join/Leave, routet aber kein Media. Ein serverseitiger Peer
  pro Teilnehmer wird erzeugt und empfängt das Browser-Video (mit heutigem SDK möglich), aber nichts wird
  weitergeleitet — sichtbar wird noch nichts, aber der gesamte Raum-/Signaling-/Lobby-/Choreografie-Pfad ist live
  und testbar.
- **`SfuRoomMediaRouter` (nach SDK 4.7.0):** je Teilnehmer-Peer via `AddTrack`/Renegotiation N-1 ausgehende
  Tracks; `AttachMediaTap` auf jedem Sender → Frame-Forwarding an die Empfänger-Tracks; PLI beim Join.
  Nur diese eine Klasse wird beim SDK-Release ergänzt — kein Umbau am Rest.

## Zerlegung (jedes Slice = eigener Plan/Umsetzung)

**Vorstufe (Ops, parallel):** `Callora-Production` auf den gemergten Stand ziehen (NuGet-Referenzen inkl.
WebRTC-Channel). Unabhängig vom Plugin-Design.

**Plattform-Verdrahtung (C-Surface, in callora):**
- **C-Surface-Render-Gate — ERLEDIGT (PR offen, `feat/surface-per-surface-access`):** `/surface/render` löst jetzt die
  konkrete `WorkspaceSurface` auf und gated auf deren **per-Surface `AccessMode`** (Public/Authenticated/**Mixed →
  Shell anonym**), Kontext (SurfaceKey/SurfaceType/Locale/Theme) aus der Surface. Neuer Store-Resolver
  `ResolveSurfaceByPublicRouteAsync`; `ResolveByPublicRouteAsync` regressionsgeprüft unverändert. Build 0/0,
  1039 Core + 29 Analyzer grün.
- **C-Surface-ui-chain — FOLLOW-UP:** `ui-chain`/Asset-Auslieferung ist noch am workspace-weiten
  `SurfaceAccessPolicy`. Im `Mixed`-Szenario kein Blocker (Workspace-Policy bleibt `Public`, per-Surface
  `Authenticated` schützt Mitglieder-Surfaces am Render-Gate). Optionaler `surfaceKey`-Parameter + per-Surface-Gate
  bei Bedarf. Der Public-HTTP-Seam (PR) liefert den anonymen JSON-Datenweg; das `WorkspaceSurface`-Modell (`Mixed`)
  den Gast-UI-Weg.

**Jetzt baubar (Multi-Track-unabhängig):**
- **P1a — Plugin-Gerüst + Raum-Domäne:** `videoconference`-Plugin scaffolden; `Room`/`Participant`/`ParticipantRole`/
  `Invitation`/`RoomToken` + EF-Persistenz + Plugin-Lifecycle/Exports + `IInvitationService` (Token mint/validieren).
  *(erledigt: P1a auf dem Plugin-Repo)*
- **P1b — Raum-Signaling-Choreografie (bis Seam):** WS-Endpunkt + Authorizer (Token single-use/TTL),
  Raum-Registry, Peer-Erzeugung (1/Teilnehmer, heutiges SDK), Join/Leave/Teilnehmerliste-Broadcast,
  Signaling-Protokoll; Peer-Übergabe an `IRoomMediaRouter` (Noop-Impl). Media-Forwarding NICHT hier.
  *(erledigt: P1b + Härtung auf dem Plugin-Repo)*
- **P-Lobby — Rollen & Admission:** `ParticipantRole` + `RoomAccessPolicy`; Admission-Zustand in der Session
  (`Admitted`/`Pending`); **Lobby-Gate** im Signaling-Handler (Pending hält ohne Peer); Knock-Broadcast an Hosts;
  Host `admit`/`reject`/`kick` als WS-Control-Messages → an den Lobby-Waiter geroutet; Rollen in der Teilnehmerliste.
- **P2 — Join-Flow, Einladungslink & Gast-Zugang (SSR-Nunjucks + Vue):** Einladungslink (workspace-gebunden, löst
  Workspace/Surface auf); **SSR-Shell** (`index.njk`, erweitert Base-Shell) mit Raum-Rahmen + Name-Formular-Gerüst;
  **dynamische Vue-View** (Lobby-Wartezustand, später Raum) eingehängt; Token-Validierung + Name-Absenden über den
  **Public-HTTP-Seam (JSON)**; `Pending`-Session-Mint. Läuft über die **`Mixed`-Raum-Surface** (öffentliche +
  geschützte Routen). Ersetzt nur den verworfenen **C#-`HtmlRenderer`** (string-HTML) — SSR bleibt, aber via Nunjucks.
  Braucht die **C-Surface**-Verdrahtung (per-Surface-Auflösung + `AccessMode`-Gate).
- **P3 — Chat:** raum-gebunden über den WS-Seam (kein DataChannel im SDK); Verlauf optional persistiert.
- **P4-Blur — Hintergrundblur:** browser-seitig (MediaPipe/WebGL auf dem lokalen Video vor dem Encoding); Frontend.
- **P5 — Admin-UI (Vue):** Räume anlegen/auflisten/beenden + Bypass-Policy im Admin-Slot
  (`extension.page.videoconference`) über den **Admin-API-Seam** (RBAC + Nav); IIFE-Bundle.

**Wartet auf SDK 4.7.0 (Multi-Track):**
- **P1c — SFU-Forwarding-Kern:** `SfuRoomMediaRouter` (Multi-Track-Peer + Frame-Routing + PLI).
- **P4-Screen — Screensharing-Track:** zweiter Video-Track (getDisplayMedia) via Renegotiation durch den SFU.
- **Multi-Video-Rendering** im Browser (mehrere Remote-Streams).

**Startsequenz:** P1a → P1b (+ Noop-Router) → P-Lobby → P2 (+ C-Surface-Verifikation) → P3/P5 parallel-fähig →
[SDK 4.7.0] → P1c/P4-Screen.

## Extensibility-Demonstration (das eigentliche Vorzeige-Ziel)
- **Surface-Vue-View(s):** die Raum-App als Surface-View (`window.calloraSurface`, Vue external), workspace-gebunden,
  für Gäste anonym über die join-Surface — zeigt Surface-Komposition und die anonyme Gast-Erreichbarkeit.
- **Admin-Slot:** die Raum-Verwaltung als Vue-IIFE-Bundle am `extension.page.<id>`-Slot über den Admin-API-Seam
  (RBAC + Nav) — zeigt das Plugin-Admin-UI-Bundle-Muster (Vue external → `window.CalloraAdmin.vue`).
- **Extension Points / Host-Seams:** WS-Contributor (Signaling + Lobby), Admin-API-Route-Registration,
  Public-HTTP-Contributor (Gäste), Business-Events (`room.participant.joined`, `room.guest.admitted` etc.) —
  zeigt, dass ein Plugin die Plattform ohne Core-Änderung erweitert.

## Decision Log
- **Workspace-gebundenes Zugangsmodell (auch für Gäste), getragen von `WorkspaceSurface`** — der Gast ist *Gast
  eines Workspaces* (anonym), nicht workspace-unabhängig. Getragen vom bestehenden Surface-Modell: eine
  `WorkspaceSurface` mit **eigener URL**, `AccessMode = Mixed` (öffentliche + geschützte Routen) und `TemplatePluginId`
  (Erweiterung der Base-Shell). Verworfen: „workspace-unabhängiger Public-App-Host" **und** die zwischenzeitliche
  Annahme, es brauche einen neuen Plattform-Seam für per-Surface-anonymen-Zugang — das kann `Mixed` schon.
- **Gast-Lobby + Host-Admission (Meet/Teams/Zoom-Modell)** — Gäste klopfen an, ein Host lässt ein/ab; Mitglieder
  können per Bypass-Policy direkt eintreten. Lobby-Gate sitzt im Signaling-Handler (Pending-Session ohne Peer).
- **Rollen Host/Teilnehmer/Gast + `RoomAccessPolicy` (WorkspaceTrusted/LockedToHost)** — minimal, YAGNI; weitere
  Policies später.
- **Lobby-Control in der Host-Raum-View, nicht im Admin-Slot** — der Host ist im Raum; spart einen Admin-Push-Kanal.
- **SSR (Nunjucks) + Vue (dynamisch), `HtmlRenderer` verworfen** — kein **C#-gebautes** HTML; die SSR-Shell rendert
  die Plattform aus dem Plugin-**Template-Bundle** (`.njk`, erweitert Base-Shell), Vue übernimmt die dynamischen
  Raum-Inhalte. Das Plugin ruft `ISurfaceRenderer` nicht selbst auf → dessen Nicht-Erreichbarkeit blockiert nicht.
- **Public-HTTP-Seam als anonymer JSON-Datenweg für Gäste** — Token trägt Workspace-/Raum-Scope, bleibt damit
  workspace-gebunden. (Beibehaltung des PR ist offene User-Entscheidung.)
- **Base-Template + Erweiterung pro Surface als Extensibility-Vorführung** — `SurfaceShell.SpaRoot` + `TemplatePluginId`
  + `@bundleId`-Includes; die Raum-App ist eine Surface-Erweiterung des Base-Templates (der „Blöcke/Extending"-Kern
  des ursprünglichen Auftrags).
- **Kein neuer Plattform-Seam für Gast-UI** — `WorkspaceSurface.AccessMode = Mixed` deckt öffentliche + geschützte
  Routen ab. Höchstens eine kleine Verifikation/Verdrahtung, dass der anonyme ui-chain/Asset-Pfad die per-Surface
  `AccessMode` ehrt (C-Surface).
- **Echter SFU statt Mesh** — SFU ist der moderne Standard; Mesh skaliert nur bis ~4-6. Preis: braucht das
  Multi-Track-SDK zuerst. (Alternativen Mesh / relayed-Mesh geprüft und verworfen.)
- **Multi-Track-Primitive ins SDK, SFU-Logik ins Plugin** — SIPSorcery-Trennung; hält das SDK generisch und die
  Anwendungslogik draußen. SDK-Release 4.7.0 (MINOR — additive WebRTC-Fläche, kein 4.6.1-Patch).
- **Eigenständiges Plugin (SDK direkt), keine Communication-Kopplung** — SFU (N-Party-Forwarding) ist ein anderes
  Muster als der Voice-Channel (1:1-Call); direkter `IPeerConnection`-Zugriff nötig.
- **Media-Seam `IRoomMediaRouter`** — entkoppelt den ganzen Nicht-Media-Bau vom SDK-Track; nur eine Klasse wird
  beim SDK-Release ergänzt.
- **Chat über WS-Signaling-Seam** — das SDK hat keinen WebRTC-DataChannel; der WS-Seam ist ohnehin da und robust.
- **Blur/Screenshare client-seitig** — Blur ist lokale Video-Verarbeitung (kein Server-Media); Screenshare ist ein
  zusätzlicher Client-Track, den der SFU wie jeden anderen forwardet.

## Offene Punkte (bei Umsetzung/Slice klären)
- **Modell des per-Surface anonymen Zugangs** (Companion): per-Surface-Policy vs. designierte Public-Surface vs.
  Route-Allowlist — eigener kurzer Design-Pass.
- **Identity-Resolution auf der join-Surface:** wie unterscheidet der Signaling-/Mint-Pfad ein authentifiziertes
  Mitglied von einem anonymen Gast, wenn beide dieselbe Surface laden (Session-Cookie/JWT vs. reiner Einladungstoken)?
- **Gast-Datenweg:** workspace-unabhängiger `/public/{pluginId}/…` (Token trägt Scope) vs. workspace-scoped
  Endpoint — Konsistenz mit dem workspace-gebundenen Modell.
- **Lobby-Timeout / Re-Knock / Kick-Semantik** (Wieder-Anklopfen nach Ablehnung?).
- **Codec-Wahl für den SFU** (VP8 vs. H264; alle Teilnehmer denselben — kein Transcoding).
- **Simulcast-Empfang** + Layer-Auswahl je Empfänger-Bandbreite (SDK 4.7.0-abhängig; v1 single-layer).
- **Raum-Kapazität / Token-TTL / Rejoin-Verhalten.**
- **Persistenz-Umfang des Chats** (flüchtig vs. Verlauf).
- **Multi-Instance/Sticky-Routing** (ein Raum = ein Prozess in v1).

## Testing
- Fast-Tests mit Fakes (`IPeerConnection`/`IWebRtcClient`, wie im Communication-WebRTC-Test): Raum-Domäne
  (Join/Leave/Kapazität), Rollen (Host/Teilnehmer/Gast), Token-Mint/-Consume (single-use/TTL, Admission-Zustand),
  **Lobby-Gate** (Pending hält ohne Peer; admit → Peer+Join; reject/Timeout → Close), **Bypass-Policy** (Mitglied
  direkt vs. Gast klopft), Signaling-Choreografie (Teilnehmerliste-Broadcast mit Rollen), Chat-Relay,
  `NoopRoomMediaRouter`-Verdrahtung, Admin-/Invitation-Routen.
- Gast-Surface: anonyme Surface-Load (ui-chain/Assets/Theme ohne 404) sobald der Companion-Seam steht.
- `SfuRoomMediaRouter` (nach SDK 4.7.0): Forwarding-Schleife mit Fake-Multi-Track-Peers; realer Browser-Interop
  als opt-in/späterer E2E (kein CI-Default).
