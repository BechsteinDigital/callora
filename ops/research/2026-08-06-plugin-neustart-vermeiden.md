# Neustart-Zwang bei Plugins — technische Recherche

**Datum:** 2026-08-06
**Status:** Recherche, keine Entscheidung
**Anlass:** Manche Plugins (Beispiel: Communication) erzwingen einen vollen Host-Neustart. Welche Wege gibt es, das zu lösen?

---

## 1. Was hier tatsächlich einen Neustart erzwingt

Wichtig vorweg, weil es die Frage verschiebt: **Callora rät nicht, ob ein Plugin entladbar ist — es misst es.**
`RuntimePluginHost.DeactivateInternalAsync` entfernt die Exporte, stoppt das Plugin, entlädt den
collectible `AssemblyLoadContext` und prüft anschließend über eine `WeakReference`, ob der Kontext
wirklich eingesammelt wurde. Gelingt das nicht, ist das Ergebnis
`RuntimePluginDeactivateStatus.Failed` mit der Meldung „a host restart is required to fully release
it".

„Braucht Neustart" ist damit heute schon eine **beobachtete Eigenschaft pro Plugin**, keine
Kategorie. Die eigentliche Frage lautet also nicht „wie machen wir Plugins entladbar", sondern
„welche Pins treten konkret auf, und welche davon lohnt sich zu beseitigen".

### Pin-Klassen, nach Realitätsgehalt in diesem Code

**(a) Geteilte Vertrags-Assemblies — dauerhaft, per Design.**
Ein unter `"contracts"` deklariertes Assembly wird einmal in den Default-Kontext geladen und bleibt
dort für die Host-Lebensdauer. Das ist kein Defekt, sondern der Preis für Typidentität über
ALC-Grenzen. Betroffen ist der *Vertrag*, nicht die Implementierung — ein Plugin kann also
ausgetauscht werden, solange sein Vertrag gleich bleibt.
Bei Communication kommt hinzu, dass `Callora.Host.Cli` die Abstractions direkt referenziert; die
liegen damit ohnehin unentladbar im Default-Kontext.

**(b) Laufende Arbeit — der wahrscheinliche praktische Blocker, und behebbar.**
Communication hält mehrere Dauerläufer: `PeriodicPacingClock`, `PacedAudioSender`, `MediaBridge`,
die WebSocket-Signalling-Handler. Solange ein Task auf einem Stack-Frame aus Plugin-Code steht, ist
der ALC gepinnt. `SafeStopAsync` reicht dem Plugin zwar ein Cancellation-Token, aber der Host
**wartet nicht darauf, dass die ausstehende Arbeit tatsächlich ausgelaufen ist**, bevor er entlädt.

**(c) Fremdreferenzen — weitgehend erledigt.**
Exporte werden über `RemoveExportsByPlugin` entfernt, Capability-Abos über
`CapabilitiesChanged -= …` abgemeldet. Diese Klasse ist im Wesentlichen adressiert.

**Kein Blocker hier:** native Bibliotheken. Das CalloraVoipSdk-Paket enthält keine nativen Artefakte
— rein managed. Native DLLs wären der harte Fall, denn sie lassen sich aus einem collectible ALC
prinzipiell nicht entladen.

> **Ungeprüft:** Welcher Pin bei Communication konkret zuschlägt, ist eine begründete Vermutung
> ((b), aus der Existenz der Dauerläufer). Belegen ließe sich das in Minuten: Plugin aktivieren,
> deaktivieren, Log-Zeile prüfen. Das wäre der erste Schritt jeder Umsetzung.

---

## 2. Wege

### Weg 1 — Drain statt Stop
Zweiphasiges Deaktivieren: erst *quiesce* (keine neue Arbeit annehmen, Tokens canceln), dann mit
Frist auf das Auslaufen der offenen Schleifen warten, dann entladen.

- **Kosten:** klein. Eine Erweiterung des Lifecycle-Vertrags (`StopAsync` bekommt die Bedeutung
  „laufe aus und komm zurück, wenn nichts mehr läuft") plus eine Frist im Host.
- **Wirkung:** adressiert genau Pin-Klasse (b), also vermutlich den realen Fall.
- **Haken:** verlagert Verantwortung ins Plugin. Ein Plugin, das seine Tasks nicht sauber abbaut,
  bleibt gepinnt — nur merkt man es dann früher und mit besserem Fehlertext.

### Weg 2 — Entladbarkeit als geprüfte Eigenschaft
Die Messung existiert schon; sie wird nur nach dem Schaden ausgewertet. Ein Konformitätstest
(aktivieren → deaktivieren → Kontext eingesammelt?) im Plugin-CI oder bei der Installation macht
Hot-Swap zu etwas, das ein Plugin **verdient**, statt zu etwas, das jeder behauptet.

- **Kosten:** klein, rein additiv.
- **Wirkung:** löst nichts, verhindert aber, dass es unbemerkt kaputtgeht — und liefert die
  Datenbasis dafür, welche Plugins überhaupt betroffen sind.

### Weg 3 — Prozess-Isolation für einzelne Plugins
Das Plugin läuft in einem eigenen Prozess, Kommunikation über lokalen Transport (gRPC, Named Pipe,
Unix-Socket). Neu gestartet wird der *Plugin-Prozess*, nicht der Host. Das Muster von VS Code
(Extension Host) und von Sidecar-Architekturen.

- **Kosten:** hoch. Serialisierungsgrenze, keine In-Process-Exporte mehr, Latenz, doppelte
  Deployment-Mechanik, Absturz-/Neustart-Semantik.
- **Wirkung:** vollständig — ein Prozess ist immer entladbar.
- **Bemerkenswerte Umkehrung:** ausgerechnet Communication, das Plugin mit dem größten
  Neustart-Problem, ist auch das mit dem besten Isolations-Profil. Es hält ohnehin langlebige
  eigene Ressourcen (SIP-Registrierungen, Media-Sockets) und spricht nach außen über
  Vertrags-Aufrufe, nicht über geteilte Objektgraphen. Für ein reines Datenlogik-Plugin wäre der
  Preis absurd, hier wäre er argumentierbar.

### Weg 4 — Den Neustart billig machen statt vermeiden
Blue/Green oder Rolling: zweiter Host-Prozess startet, übernimmt hinter dem Frontdoor, alter Prozess
läuft aus. Für ein self-hosted Produkt oft die *richtige* Antwort — man repariert nicht die
Entladbarkeit, man macht den Neustart unsichtbar.

- **Rückenwind aus der Surface-Arbeit:** Surface-Sessions liegen serverseitig und sind widerrufbar,
  Handoff-Tickets existieren. Ein Prozesswechsel kostet also keine Anmeldung.
- **Haken, speziell für Communication:** laufende Calls und Media-WebSockets überleben einen
  Prozesswechsel nicht. Man braucht dieselbe Drain-Primitive wie Weg 1, nur eine Ebene höher
  (keine neuen Calls annehmen, bestehende auslaufen lassen, dann umschalten).
- **Kosten:** mittel, aber Deployment-seitig, nicht architektur-seitig.

### Weg 5 — Erweiterungen ohne Code
Ein Teil dessen, was heute „Plugin" heißt, bringt gar keine Assembly mit: Themes,
Surface-Templates, Flows, Konfiguration. Wo nichts geladen wird, ist auch nichts zu entladen.
Eine Trennung in **Code-Plugins** und **Content-Plugins** nimmt einen Teil des Problems weg, statt
ihn zu lösen.

- **Kosten:** klein bis mittel (Klassifikation + eigener Installationspfad).
- **Wirkung:** reduziert die Menge der Fälle, adressiert den harten Kern nicht.

### Weg 6 — Verträge nebeneinander statt ersetzen
Der Pin liegt am Vertrag. Statt ihn zu entladen: ihn nie ersetzen müssen. Additive-only innerhalb
einer Major, und bei einem Bruch ein **zweites Assembly daneben** (`…Contracts` v1 und v2), das der
Host zusätzlich lädt. Ein Konsument wechselt, wenn er bereit ist.

- **Kosten:** der Anbieter muss zwei Gesichter eine Weile parallel bedienen.
- **Wirkung:** macht Vertrags-Updates ohne Neustart möglich — der häufigste Grund, warum ein
  Neustart überhaupt nötig wäre.
- **Passt direkt auf den frisch gebauten Katalog:** der zeigt bereits, wer an welcher Version hängt,
  also wann ein v1 abgeräumt werden darf.

### Weg 7 — WASM-Komponenten (Fernziel, zur Einordnung)
Plugins als WASM-Komponenten mit definierter Schnittstelle (wasmtime/WASI). Echt entladbar, echt
sandboxed, ALC-Problem existiert nicht.

- **Kosten:** enorm. Kein EF, kein ASP.NET, alles über die Schnittstelle; für .NET-Plugins heute
  kein realistischer Weg.
- **Warum trotzdem nennen:** es ist die Richtung, in die sich Erweiterungs-Ökosysteme bewegen, und
  es markiert die Obergrenze dessen, was Isolation kosten kann.

---

## 3. Der VoIP-/WebRTC-Fall im Besonderen

Hier trennen sich zwei Fragen, die oben noch als eine behandelt wurden:

- **Entladbarkeit** — kann der Prozess weiterlaufen, während das Plugin getauscht wird?
- **Sitzungs-Überleben** — überlebt ein *laufendes Gespräch* den Tausch?

Für Medien ist die zweite Frage die relevante, und **die erste beantwortet sie nicht**. Selbst ein
perfekter Hot-Swap würde jedes laufende Gespräch beenden: `CommunicationPlugin.StopAsync` disposed
`_ownedWebRtcClient`, `_conferenceMediaProvider` und `_ownedVoipClient`, und damit reißen ICE, DTLS
und RTP ab. Der Zustand einer lebenden Sitzung — DTLS-Schlüssel, SRTP-Zähler, das gewählte
Candidate-Pair, der UDP-Socket — liegt in den SDK-Peers und lässt sich nicht übergeben. Für den
Media-Pfad ist ALC-Entladbarkeit also **die falsche Baustelle**.

### 3.1 Was ein Neustart heute kostet

Mehr als eine Unterbrechung. `CallControlService.DisposeAsync` finalisiert jeden aktiven Call mit
`CallOutcome.Failed` und der Begründung „The host shut down while the call was active." Ein Neustart
ist damit kein Rauschen, sondern ein Datenereignis: die Historie schreibt gescheiterte Gespräche,
die technisch nur unterbrochen wurden.

### 3.2 Der Hebel: die Hälfte der Lösung ist schon gebaut — im falschen Repo

`callora-videoconference/.../lib/room-controller.ts` implementiert bereits vollständig, was ein
Neustart-überlebender Client braucht:

- ein **Resume-Token** mit Deadline (`canResume()`),
- **Reconnect mit Backoff** (`scheduleReconnect`, `RECONNECT_BACKOFF_MS`),
- einen Zustand `reconnecting`, der **Kacheln und Layout stehen lässt**, statt den Raum zu schließen,
- Verwerfen der PeerConnection vor dem Retry, weil der Server auf dem frischen Socket neu anbietet,
- und die Unterscheidung „aufgelegt" (Token verfällt) vs. „Verbindung weg" (Token gilt weiter).

Der Client behandelt einen abgerissenen Socket also bereits als heilbar. Was ihn über einen
Host-Neustart trotzdem tötet, ist **serverseitig**: `RoomSignalingSessionStore` ist eine
`ConcurrentDictionary` im Prozess, `RoomRegistry` ebenso. Nach dem Neustart kennt der Server das
Resume-Token nicht mehr, der Reconnect läuft in den Zweig „token the server has already forgotten",
und der Zustand fällt auf `closed`.

**Damit ist die Lücke eine einzige: der Resume-Zustand ist flüchtig.** Überlebte er den Prozess,
sähe ein Neustart für den Teilnehmer aus wie ein Tunnel — ein paar Sekunden „Verbinde erneut…",
dann wieder derselbe Raum, dieselbe Teilnehmerliste, neu ausgehandelte Medien. Die Neuaushandlung
ist keine neue Mechanik: `ConferenceMediaRouter.FireRenegotiation` läuft heute bei jedem Join und
Leave. Ein zurückkehrender Teilnehmer ist für den SFU schlicht ein Join.

Zwei Ehrlichkeiten dazu:

- Der Store dokumentiert selbst, dass Raum-Affinität eine **Deployment-Anforderung** ist — alle
  Teilnehmer eines Raums müssen dieselbe Instanz erreichen. Das Token dauerhaft zu machen ist
  *nicht* dasselbe wie den Store zu verteilen. Für ein Single-Instance-Self-Hosting ist der Punkt
  gegenstandslos, für mehrere Instanzen bleibt er bestehen.
- Der Gewinn ist nicht auf Neustarts beschränkt. Derselbe Pfad deckt den Fall ab, der real viel
  häufiger auftritt: WLAN-Wechsel, Tunnel, Mobilfunkloch.

### 3.3 Die Asymmetrie: Communication hat kein Resume

`Communication/.../WebRtcSignalingSessionStore` kennt kein Resume — Tokens sind single-use und
werden beim Auflösen entfernt. Ein WebRTC-**Sprach**anruf im Browser hat also überhaupt keinen
Wiederverbindungspfad, während der Videokonferenz-Client einen ausgebauten hat. Das jüngere Plugin
hat gebaut, was dem älteren fehlt.

Das spricht dafür, Resume **nicht** ein drittes Mal pro Plugin zu bauen, sondern an der
WebSocket-Fläche des Hosts: ein Contributor deklariert seine Sitzung als resumierbar und liefert den
Wiederherstellungs-Callback, der Host besitzt Token, Deadline und den Store. Dann erbt jede künftige
Realtime-Fläche das Verhalten.

### 3.4 SIP ist der harte Fall — und hat eine andere Antwort

Für SIP gibt es kein Resume-Analogon. Die Gegenstelle ist ein Carrier, kein eigenes JS; der Dialog
hängt an Registrierung, Tags, CSeq und Route-Set im SDK-Client. Die realistische Antwort ist hier
nicht Wiederherstellen, sondern **Auslaufen**, und dafür gibt es ein etabliertes Vorbild: Asterisk
unterscheidet `core stop now` / `core stop gracefully` (keine neuen Anrufe, bestehende zu Ende) /
`core stop when convenient` (warten bis null Anrufe); FreeSWITCH nennt es `shutdown elegant`. Genau
diese Semantik fehlt Callora auf Plugin-Ebene.

Heute gibt es keinen Drain-Zustand: `SafeStopAsync` ruft `StopAsync` und entlädt sofort. Die
`CommunicationReadinessProbe` ist eine *Auskunft*, kein *Schalter* — nichts kann das Plugin in
„nimmt nichts Neues mehr an" versetzen. Ein solcher Zustand ist klein zu bauen und zahlt doppelt: er
bedient den Plugin-Tausch (Weg 1) und den Prozess-Neustart (Weg 4) mit derselben Primitive.

**Die SDK-Seite ist dafür schon vorhanden und wird nicht genutzt.** Geprüft an
CalloraVoipSdk 4.7.3 (und identisch in 4.6.0):

| Baustein | SDK-Fläche | Von Callora genutzt |
| --- | --- | --- |
| Registrierung einzeln zurückziehen | `IPhoneLine.UnregisterAsync(ct)` | nein |
| Registrierung über den Line-Manager zurückziehen | `IPhoneLineManager.UnregisterAsync(LineId, ct)` | nein |
| INVITE im Rennfenster ablehnen | `Call.RejectAsync(statusCode, reasonPhrase, ct)` | ja, aber ohne Code |
| Zeitweise unregistrierte Leitung modellieren | `LineState.Unregistered` / `Reconnecting` | ja (nur lesend) |

`SdkVoiceChannel.Dispose` hängt sich lediglich von `IncomingCall` und `StateChanged` ab — es
**deregistriert nie**. Die Registrierung stirbt also mit dem Socket, und der Carrier schickt INVITEs
weiter ins Leere, bis sein eigener Timeout greift. `ISdkVoiceRuntime` bietet dafür auch keinen Platz:
die Naht kennt genau zwei Operationen (`ConnectAsync`, `CreateMediaTap`).

Konkret fehlt damit callora-seitig:

1. eine Quiesce-Operation an `ISdkVoiceRuntime`/`IVoiceChannel`, die `IPhoneLine.UnregisterAsync`
   ruft, ohne den Client zu disposen;
2. ein Statuscode an `ICall.RejectAsync` — `SdkCall.RejectAsync` ruft die SDK-Methode heute ohne
   Argumente und landet damit auf dem Default `486 Busy Here`. Im Drain ist `503 Service Unavailable`
   die richtige Antwort, weil der Carrier darauf zur nächsten Route wechselt statt den Anrufer zu
   verwerfen;
3. die Warteschleife auf null aktive Calls — die Zählung existiert bereits in
   `CallControlService._active`.

**Kein SDK-Feature nötig.** Der einzige Punkt, an dem das SDK wirklich an eine Grenze stieße, ist die
*andere* Frage: ein laufendes SIP-Gespräch über den Neustart hinweg zu retten. Das bräuchte Export
und Import von DTLS-SRTP- und ICE-Zustand oder eine Medienübergabe per Re-INVITE aus einem zweiten
Prozess. Dafür gibt es keine Fläche — es ist aber auch nicht der Plan: für SIP ist Auslaufen die
Antwort, nicht Wiederherstellen.

### 3.5 Korrektur zu Weg 4 (Blue/Green)

Der Frontdoor ist Caddy, ein Prozesswechsel hinter ihm ist deployment-seitig lösbar — **aber nur für
HTTP und WebSocket**. Der Medienpfad geht nicht durch den Frontdoor: das SDK bindet seinen eigenen
UDP-Endpunkt (`webRtcOptions.LocalEndPoint`). Ein zweiter Prozess kann diesen Socket nicht
übernehmen, und selbst mit `SO_REUSEPORT` verteilt der Kernel nach Hash statt nach Sitzung. Blue/Green
verschiebt für Medien also nichts — es braucht denselben Resume-Pfad aus §3.2. Weg 4 ist damit eine
Antwort für die Steuerebene, nicht für die Medienebene.

### 3.6 Wenn es einmal perfekt sein muss: Steuer- und Medienebene trennen

Communication ist heute beides in einer Assembly und einem Prozess: Call-Control, Admin-Routen,
Historie, MCP-Tools und Flows (ändern sich oft) neben SFU und Sprachkanälen (dürfen nie
unterbrochen werden). Genau an dieser Naht trennen die etablierten Systeme — Kamailio neben
RTPengine, mediasoup-Worker neben der App, Janus neben seinem Gateway. Läge die Medienebene in einem
eigenen, langlebigen Prozess, wäre die Steuerebene beliebig redeploybar.

Der Preis ist hoch (eigene Prozess-Lifecycle, Transportgrenze, Absturzsemantik). Das ist die Antwort
für den Tag, an dem Callora Konferenzen für fremde Geschäfte fährt — nicht für heute.

---

## 4. Einschätzung

Wenn ich priorisieren müsste — als Vorschlag, nicht als Entscheidung:

Nach dem Blick in den Media-Code verschiebt sich die Reihenfolge: der spürbarste Gewinn liegt nicht
beim Entladen, sondern beim Überleben der Sitzung.

1. **Resume dauerhaft machen** (§3.2). Höchster Hebel, kleinster Eingriff: der Client kann es
   bereits, es fehlt ein Store, der den Prozess überlebt. Ein Neustart wird damit vom Abbruch zur
   Verzögerung — und derselbe Pfad deckt Netzwechsel und Funklöcher mit ab.
2. **Drain als Zustand** (§3.4 / Weg 1). „Nimmt nichts Neues mehr an" ist die Primitive, die sowohl
   der Plugin-Tausch als auch der Prozess-Neustart braucht. Ohne sie ist jeder andere Weg ein
   harter Schnitt.
3. **Neustart nicht als Fehler verbuchen** (§3.1). `CallOutcome.Failed` mit „host shut down" für ein
   Gespräch, das nur unterbrochen wurde, verschmutzt die Historie. Ein eigener Ausgang kostet
   fast nichts.
4. **Resume an die Host-WebSocket-Fläche heben** (§3.3), damit Communication und jede künftige
   Realtime-Fläche es erben, statt es ein drittes Mal zu bauen.
5. **Messen** (Deaktivierung von Communication, Log-Zeile lesen) und danach **Weg 2** — Entladbarkeit
   als geprüfte Eigenschaft. Das bleibt richtig, ist für Medien aber zweitrangig.
6. **Weg 6**, sobald der erste Vertragsbruch ansteht — der Katalog liefert dafür schon die Daten.
7. **Weg 4** als Betriebsantwort für die Steuerebene, mit der Einschränkung aus §3.5.
8. **Weg 3 / §3.6** nur, wenn Konferenzen einmal fremdes Geschäft tragen. Heute nicht.

Weg 7 ist Kontext, kein Kandidat.
