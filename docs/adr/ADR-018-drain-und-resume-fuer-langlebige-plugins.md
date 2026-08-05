# ADR-018 — Drain und Resume für langlebige Plugins

**Status:** Accepted
**Datum:** 2026-08-06
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* ops/research/2026-08-06-plugin-neustart-vermeiden.md — die Recherche, aus der diese Entscheidung folgt
* ADR-012 — Ein-Core-Extensibility (domänen-neutrale Plattform)
* ADR-016 — Neutrale Realtime-Media-Abstraktion (Communication als einziger SDK-Binder)
* ADR-017 — Surface-Identität und Session-Transport (Session-Mechanik im Host)

---

## 1. Kontext

Ein Plugin lebt in einem eigenen `AssemblyLoadContext` und kann zur Laufzeit
installiert, aktiviert und entfernt werden. Für die meisten Plugins reicht das:
`StopAsync`, entladen, fertig.

Für Plugins mit **langlebiger Arbeit** reicht es nicht. Communication hält
SIP-Registrierungen, laufende Gespräche, Media-Sockets und Konferenz-Sitzungen.
Ein Tausch oder Neustart trifft dort nicht nur Code, sondern Menschen, die gerade
miteinander sprechen.

Die Recherche hat zwei Befunde geliefert, die die Fragestellung verschieben:

**Erstens: Entladbarkeit löst das Problem nicht.** Selbst ein perfekter Hot-Swap
beendet jedes laufende Gespräch, weil DTLS-Schlüssel, SRTP-Zähler, das gewählte
ICE-Candidate-Pair und der UDP-Socket im SDK-Peer liegen und nicht übergeben werden
können. Wer Gespräche schützen will, muss an einer anderen Stelle ansetzen als am
Load-Context.

**Zweitens: die beiden Modalitäten brauchen gegensätzliche Antworten.**

* Bei **SIP** ist die Gegenstelle ein Carrier. Es gibt keinen Weg, ein Gespräch
  wiederherzustellen — die Antwort ist **Auslaufen**.
* Bei **WebRTC** gehören beide Enden der Signalisierung uns. Ein Client kann sich
  wiederverbinden und neu aushandeln — die Antwort ist **Wiederaufnahme**.

Der VideoConference-Client hat die zweite Antwort bereits vollständig gebaut
(Resume-Token, Backoff, `reconnecting`-Zustand, der Kacheln stehen lässt). Was fehlt,
ist die serverseitige Hälfte: der Zustand lebt in einer `ConcurrentDictionary` und
stirbt mit dem Prozess.

---

## 2. Entscheidung

Der Host bekommt **zwei Primitiven**, keine allgemeine Isolationslösung.

### 2.1 Drain — ein Zustand vor dem Stop

`IDrainablePlugin` ist ein **optionaler** Zusatzvertrag zu `IHostManagedPlugin`:

```csharp
public interface IDrainablePlugin
{
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}
```

Der Vertrag hat genau eine Bedeutung: **nimm keine neue Arbeit mehr an und komm
zurück, wenn die offene Arbeit ausgelaufen ist.**

Der Host ruft ihn vor `StopAsync` mit einer Frist
(`CalloraHostingOptions.PluginDrainTimeout`, Default 30 Sekunden). Läuft die Frist ab,
wird das protokolliert und trotzdem gestoppt — ein Plugin darf eine Deaktivierung
verzögern, nicht verhindern.

**Reihenfolge:** Drain → Exporte entfernen → `StopAsync` → Entladen. Der Drain läuft
mit intakten Exporten, weil auslaufende Arbeit sie noch braucht.

**Was der Host nicht kann:** entscheiden, was „neue Arbeit" ist. Diese Grenze kennt
nur das Plugin. Der Host besitzt die Frist, das Plugin die Bedeutung.

Ein Plugin ohne den Vertrag verhält sich exakt wie bisher.

### 2.2 Resume — eine Zusage, keine Sitzung

Eine wiederaufnehmbare Realtime-Sitzung wird **nicht** serialisiert. Sie kann es
nicht: `WebRtcSignalingSession` hält SDK-Objekte, `RoomRegistry` hält offene Sockets.

Persistiert wird stattdessen eine **Wiederaufnahme-Zusage**: Token, Deadline,
besitzendes Plugin und ein für den Host **opaker** fachlicher Payload. Meldet sich ein
Client mit dem Token, gibt der Host dem Contributor den Payload zurück und der baut
seine Sitzung **neu auf**. Genau das tut der VideoConference-Client bereits: er
verwirft die PeerConnection und lässt den Server auf dem frischen Socket neu anbieten.

Der Host besitzt Token, Deadline, Einlösung und Store. Das Plugin besitzt die
Bedeutung des Payloads. Damit erbt jede künftige Realtime-Fläche das Verhalten, statt
es ein drittes Mal zu bauen.

**Der Payload wird verschlüsselt gespeichert.** Er ist für den Host opak — also kann
der Host nicht beurteilen, wie sensibel er ist. Ein Konferenzplatz trägt technische
IDs, eine Behandlungssitzung womöglich eine Patientenreferenz. Unbedingtes Schützen
kostet eine Runde Data Protection und erspart die Frage. Der Purpose trägt die
Plugin-Id, damit die Plugin-Bindung nicht nur ein Abfrageprädikat ist, sondern
kryptografisch hält.

**Ein Ticket beschreibt eine Identität, es erteilt keine.** Wer das Token hat, kann
den Versuch machen — die Plugin-Bindung hält ein anderes *Plugin* ab, nicht einen
anderen *Client*. Deshalb gehört in den Payload das Subjekt des Aufrufers, an den das
Ticket ausgegeben wurde, und beim Einlösen muss der Konsument es gegen den aktuellen
`HostWebSocketConnectRequest.Caller` prüfen. Auch ein erkannter Gast hat ein stabiles
Subjekt (ADR-017 §3), diese Prüfung ist also nicht auf angemeldete Nutzer beschränkt.
Wo überhaupt kein Aufrufer existiert — ein Out-of-Process-Client mit Token und sonst
nichts —, bleibt es Bearer, und das kurze Fenster ist die einzige Schranke. Der Host
kann diese Prüfung nicht für den Konsumenten übernehmen, weil nur der Konsument weiß,
was sein Payload bedeutet; er kann sie nur ermöglichen und vorschreiben.

### 2.3 Was ausdrücklich nicht entschieden wird

* **Keine Prozess-Isolation pro Plugin.** Die Kosten (Serialisierungsgrenze,
  doppelte Deployment-Mechanik, Absturzsemantik) sind heute nicht gerechtfertigt.
* **Keine Medien-Übergabe zwischen Prozessen.** Ein laufendes SIP-Gespräch überlebt
  keinen Neustart. Das ist eine akzeptierte Grenze, keine offene Aufgabe.
* **Keine verteilten Session-Stores.** Raum-Affinität bleibt eine
  Deployment-Anforderung. Ein Token dauerhaft zu machen ist nicht dasselbe, wie den
  Store zu verteilen.

---

## 3. Konsequenzen

**Ein Neustart wird von einem Abbruch zu einer Verzögerung.** Für WebRTC-Teilnehmer
sieht er aus wie ein Tunnel: ein paar Sekunden „Verbinde erneut…", dann derselbe Raum.
Für SIP-Gespräche laufen die bestehenden aus, statt mitten im Satz zu enden.

**Der Gewinn ist nicht auf Neustarts beschränkt.** Der Resume-Pfad deckt den Fall ab,
der real viel häufiger auftritt: WLAN-Wechsel, Tunnel, Funkloch.

**Die Drain-Frist muss zum Shutdown-Timeout passen.** Beim Prozess-Shutdown begrenzt
`HostOptions.ShutdownTimeout` (ASP.NET-Default ebenfalls 30 Sekunden) das Warten. Wer
die Drain-Frist hochsetzt, muss beides tun — sonst schneidet der Host-Shutdown das
Auslaufen ab.

**Ein Neustart ist kein Fehler mehr.** `CallOutcome.Interrupted` trennt „unterbrochen"
von „gescheitert", damit die Historie nach einem Deployment nicht voller gescheiterter
Gespräche steht, die technisch nur unterbrochen wurden.

**Entladbarkeit bleibt eine gemessene Eigenschaft.** `DeactivateInternalAsync` prüft
weiterhin über eine `WeakReference`, ob der Load-Context eingesammelt wurde. Der Drain
verbessert die Chance darauf spürbar, weil auslaufende Schleifen der wahrscheinlichste
Pin sind — aber die Messung bleibt die Wahrheit, nicht die Absicht.

---

## 4. Alternativen, die verworfen wurden

**Blue/Green statt Drain.** Verschiebt für Medien nichts: der Medienpfad geht nicht
durch den Frontdoor, das SDK bindet seinen eigenen UDP-Endpunkt, und ein zweiter
Prozess kann diesen Socket nicht übernehmen. Bleibt eine gute Antwort für die
Steuerebene und setzt denselben Drain voraus.

**Steuer- und Medienebene in getrennte Prozesse.** Strukturell die stärkste Antwort
und das, was etablierte Systeme tun (Kamailio neben RTPengine, mediasoup-Worker neben
der App). Aufgeschoben, nicht verworfen: das ist die Entscheidung für den Tag, an dem
Callora Konferenzen für fremdes Geschäft fährt.

**Ein Drain-Vertrag mit Zählung statt Warten** (`OutstandingWork`-Property, Host
pollt). Verschiebt die Fristlogik in den Host und erzwingt Polling. Eine
`DrainAsync`-Methode mit Host-eigenem Token drückt dasselbe idiomatisch aus und lässt
dem Plugin die Wahl, wie es wartet.
