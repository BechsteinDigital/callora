# ADR-017 — Surface-Identität und Session-Transport (Kontext immer, Identität optional)

**Status:** Accepted
**Datum:** 2026-08-05
**Entscheidungsträger:** Bechstein.Digital / Callora
**Bezug:**

* Issue #125 — Epic „Surfaces als komponierbare Arbeitsplätze", Baustein **A**
* ADR-014 — Surface-Engine (§5 Surfaces, §6.1 Access Modes)
* ADR-015 — Surface-Template-Engine (§8 Render-Kontext-Allowlist)
* ADR-012 — Ein-Core-Extensibility (domänen-neutrale Plattform)
* ADR-013 — Plugin-Trust-Modell (Trusted-in-Process)

> **Supersedes (teilweise):** Dieses ADR löst **ADR-014 §6.2 „Surface-Login"** und
> **ADR-014 §6.4 „Identity und Profile"** für Surfaces ab. Dort lag die fachliche
> Identität samt Credentials, Memberships und Principal-Profilen im Core. Callora ist
> seither ausdrücklich domänen-neutral (ADR-012): ein Kunden-, Patienten- oder
> Mitgliedermodell gehört dem Plugin, dem die Daten gehören. Der Core transportiert
> Identität, er definiert sie nicht. Die Access-Modes aus **§6.1 bleiben unverändert
> gültig** und werden hier nur präzisiert.

---

## 1. Kontext

Ein Workspace wird durch zugewiesene Plugins ausgestattet und über Surfaces
ausgeliefert. Damit daraus ein gemeinsamer Arbeitsplatz statt einer Sammlung
getrennter Anwendungen wird, muss der Host wissen, **wer** eine Surface gerade
benutzt — und diese Antwort konsistent durch Rendering, HTTP-API und
WebSocket-Upgrades tragen.

Heute kennt die Plattform im Wesentlichen zwei Zustände: authentifizierter
Backend-Principal oder anonym. `SurfaceContext` trägt `workspaceKey` und
`surfaceKey`, `SurfaceRenderContext` zusätzlich Tenant, Surface-Typ, Locale und
Theme-Tokens — aber keine Identität.

**Konkreter Auslöser.** Das VideoConference-Plugin startet aus einer Surface heraus
eine Konferenz und ruft dafür die Admin-API auf
(`window.__CALLORA_ADMIN_API_BASE__ ?? '/api/ext/admin/plugins/videoconference'`).
Das funktioniert nur, weil der aktuelle Host zufällig zugleich Admin-Nutzer ist.
Das ist eine Brücke über eine fehlende Nahtstelle, keine Architektur.

**Zwei legitime Nutzergruppen.** Eine Surface hat interne Nutzer (Mitarbeiter am
Agent-Desktop, Arzt im Praxisarbeitsplatz) — das sind Plattform-Nutzer, für die der
Host-Principal die richtige Identität ist; ein Plugin, das Mitarbeiter ein zweites
Mal authentifiziert, wäre absurd. Und sie hat externe Nutzer (Kunden, Patienten,
Leads) — dafür braucht es ein Plugin. Beides muss nebeneinander möglich sein.

**Der häufigste Fall ist keiner von beiden.** Zwischen „angemeldet" und „anonym"
liegt der wiedererkannte Gast: anonymer Warenkorb, Gastbestellung, ein halb
ausgefülltes Formular über zwei Seitenaufrufe. Ohne stabiles Subjekt kann ein Plugin
dafür keinen Zustand führen — es hat keinen Schlüssel, an dem es ihn ablegt.
Shopware löst das über das Sales-Channel-Kontext-Token, das es für Gäste genauso
gibt wie für Kunden.

---

## 2. Entscheidung

1. Ein **Surface-Kontext existiert immer**, auch anonym. Er trägt ein stabiles,
   cookie-gebundenes Subjekt und dessen Issuer. Eine **Identität hängt optional**
   daran; erst sie sagt „das ist Kunde X".
2. Gast und Authentifiziert sind **im Typ** unterscheidbar, nicht per Konvention.
3. Ein Plugin kann sich als **Identitätsanbieter** registrieren
   (`IHostSurfaceIdentityProvider`). Der öffentliche Vertrag erhält **keinen
   unbeschränkten `HttpContext`**, sondern nur ausdrücklich deklarierte
   Credential-Quellen.
4. Die **Bindung an eine Surface ist Operator-Zuweisung** nach dem Theme-Muster,
   gefiltert über die Plugin-Fähigkeit `surface.identity`.
5. Ohne gebundenen Anbieter leitet der Host aus einem vorhandenen Backend-Principal
   eine Identität mit Issuer `callora.host` ab — **ohne Admin-Berechtigungen**.
   Eine Plugin-Bindung hat immer Vorrang.
6. Der Host normalisiert jedes Ergebnis, bindet es an Tenant, Workspace, Surface und
   Audience und transportiert es in Rendering, Browser-Kontext, Surface-API und
   WebSocket-Autorisierung.

---

## 3. Zweistufiges Modell

```text
SurfaceCaller                        ← existiert IMMER
├── Subject   (Issuer + SubjectId)   ← immer, stabil, cookie-gebunden
└── State
    ├── Guest                        → kein Identity-Objekt
    └── Authenticated                → Identity ist vorhanden
                                        ├── DisplayName
                                        ├── Claims (namespaced)
                                        ├── AuthenticationMethod
                                        ├── AuthenticatedAtUtc
                                        └── ExpiresAtUtc
```

`SurfaceCaller` ist eine geschlossene Typhierarchie (`GuestSurfaceCaller`,
`AuthenticatedSurfaceCaller`); ein dritter Fall kann außerhalb des Core nicht
entstehen. Der Grund ist nicht Stilfrage: Käme beides als „hat ein Subjekt" an,
prüft irgendwann ein Plugin auf *Vorhandensein* statt auf *Authentifizierung* und
hängt eine Berechtigung an ein Gast-Token, das jeder selbst erzeugen kann. Ein
Plugin muss den Fall unterscheiden, um an die Identität zu kommen.

**Der Issuer wird nie wegabstrahiert.** Ein Plugin bekommt immer
`(Issuer, SubjectId, Claims)` — nie nur `SubjectId`. Sonst kann ein anderer Anbieter
dieselbe Subject-Id ausstellen und das konsumierende Plugin merkt es nicht. Die
stabile Identität ist `Issuer + SubjectId`.

Reservierte Issuer des Hosts:

| Issuer | Bedeutung |
|---|---|
| `callora.surface-guest` | anonymer, wiedererkennbarer Gast |
| `callora.host` | abgeleitet aus einem Backend-Principal |

Ein Plugin-Anbieter darf keinen Issuer unter `callora.` ausstellen.

### 3.1 Claims

Claims sind namespaced und versionierbar (`crm.roles`, `teleclinic.patient-id`).
Der Namespace `callora.` ist dem Host vorbehalten. Der Host validiert Form und
Grenzen, **interpretiert aber keinen einzigen Claim**: was `crm.roles` bedeutet,
entscheidet das CRM-Plugin.

---

## 4. Anbieter-Vertrag

```text
IHostSurfaceIdentityProvider
├── PluginId
├── CredentialSources : IReadOnlyList<SurfaceIdentityCredentialSource>
│     (Header oder Cookie, jeweils mit Namen — deklarierter Pluginvertrag
│      und damit Review-/Consent-Material)
└── AuthenticateAsync(HostSurfaceIdentityRequest, CancellationToken)
      → HostSurfaceIdentityResult  (Anonymous | Identified)
```

`HostSurfaceIdentityRequest` trägt Tenant-, Workspace- und Surface-Kontext,
normalisierte Request-Metadaten (Pfad, Methode, Locale, Origin) und **ausschließlich
die Werte der deklarierten Credential-Quellen**. Kein `HttpContext`, keine
Roh-Header-Collection, keine Cookie-Collection.

Der Aufruf läuft unter einer harten Ausführungsfrist (Default 2 s). Eine
Zeitüberschreitung, eine Exception oder ein ungültiges Ergebnis sind **kein
anonymer Durchlauf**, sondern ein Anbieterfehler (§6).

---

## 5. Bindung: wer ist der Anbieter dieser Surface?

Zwei bereits existierende Mechaniken werden kombiniert — es entsteht keine dritte.

**5.1 Fähigkeit.** Ein Login-Plugin deklariert in seiner `registry.json` die
Capability `surface.identity`. Das Admin-Dropdown filtert danach, statt alle
installierten Plugins anzubieten.

**5.2 Zuweisung.** Die Bindung liegt auf `WorkspaceSurface`, exakt nach dem
Theme-Muster — aber als vollständiges Quartett:

```text
IdentityPluginId
IdentityVersion
IdentityAssignedBy
IdentityAssignedAtUtc
```

Bei einem Theme ist „wer hat wann zugewiesen" Komfort. Bei Identität ist es
auditrelevant.

Surface-Keys als Plugin-Deklaration (analog `SurfaceView.surfaceKeys`) scheiden
aus: Surface-Keys sind **Operator-Daten**. Ein ausgeliefertes Login-Plugin kann sie
nicht kennen, weil die Surface erst später vom Kunden angelegt wird. Der Vergleich
mit `SurfaceView.surfaceKeys` trägt nicht — dort provisioniert das Plugin seine
Surface selbst und kennt den Key deshalb.

**5.3 Präzedenz.** Ist ein Plugin-Anbieter gebunden, gewinnt er. Die Host-Quelle
(§7) greift **nur**, wenn keine Bindung besteht. Sonst gäbe es zwei gleichzeitig
gültige Identitäten und undefiniertes Verhalten.

---

## 6. Fehler- und Randfälle

Diese drei Fälle sind Vertrag, nicht Implementierungsdetail.

### 6.1 Keine Zuweisung vorhanden

Das Verhalten hängt am `SurfaceAccessMode`:

| Access Mode | Verhalten ohne Anbieter |
|---|---|
| `Public` | braucht keinen Anbieter; Gast-Kontext wie immer |
| `Mixed` | läuft anonym; geschützte Routen lehnen ab |
| `Authenticated` | **nicht bedienbar** — definierter Fehler, kein anonymer Durchlauf |

Die Host-Quelle aus §7 ist dabei ein gültiger Anbieter: eine `Authenticated`-Surface
ohne Plugin-Bindung, aber mit Backend-Principal, ist bedienbar.

### 6.2 Zugewiesenes Plugin deaktiviert, entfernt oder nicht verfügbar

**Kein Rückfall auf anonym.** Ein fehlendes Theme fällt aufs Basis-Theme zurück —
das ist kosmetisch. Ein fehlender Identitätsanbieter wäre ein Zugriffsleck.

Die Surface **schließt für authentifizierte Zugriffe** und macht den Grund im Admin
sichtbar. Der `IPluginAvailabilityEvaluator` gehört in dieselbe Prüfung: nicht im
Workspace verfügbar heißt kein Anbieter — Entitlement, Runtime-Health und
Workspace-Aktivierung wirken damit unverändert mit.

Insbesondere greift in diesem Zustand **nicht** die Host-Quelle: eine bestehende
Bindung, die gerade nicht erfüllbar ist, ist etwas anderes als keine Bindung.

### 6.3 Anbieterwechsel

Wechselt `IdentityPluginId` oder `IdentityVersion`, werden **ausgestellte Tokens
ungültig**. Wenn eine andere Instanz für die Identität bürgt, wäre Weitervertrauen
inkonsequent. Jede Surface-Session trägt dafür die Anbieter-Provenienz mit; die
Zuweisung erhöht zusätzlich eine Generation, die serverseitige Massen-Invalidierung
ohne Zeilenscan erlaubt.

Gast-Kontexte bleiben davon unberührt — sie bürgen für nichts.

---

## 7. Host-Identitätsquelle (`callora.host`)

Ohne gebundenen Plugin-Anbieter leitet der Host aus einem bereits authentifizierten
Backend-Principal eine Surface-Identität ab.

Zwei Einschränkungen sind Teil der Entscheidung:

1. **Keine Admin-Berechtigungen als Claims.** Die abgeleitete Identität trägt
   Subjekt, Anzeigename und Workspace-Zugehörigkeit — sonst nichts. Landen
   Admin-Permissions in den Surface-Claims, prüft irgendwann ein Plugin darauf und
   eskaliert versehentlich Rechte, die nie für die Surface gemeint waren.
2. **Nachrang.** Sie greift nur bei fehlender Bindung (§5.3).

Der Nutzen ist nicht nur Pragmatismus: Ohne Host-Quelle ist die Surface-Seam bis zum
ersten Identitäts-Plugin tote Infrastruktur, und niemand baut ein Plugin für eine
Nahtstelle, die noch keiner benutzt. Mit ihr ist VideoConference der erste echte
Konsument und die Nahtstelle sofort erprobt.

---

## 8. Session-Transport

### 8.1 Zwei Speicherformen für zwei Autoritätsgrade

| | Gast-Kontext | Authentifizierte Session |
|---|---|---|
| Speicherung | zustandslos, signiert/verschlüsselt im Cookie | serverseitiger Datensatz, opake Id im Cookie |
| Autorität | keine | trägt die Identität |
| Widerruf | nicht nötig | serverseitig, sofort |
| Lebensdauer | lang (Default 30 Tage) | kurz: `min(Anbieter-Ablauf, Host-Maximum)` |

Ein Gast-Kontext erzeugt bewusst **keine Datenbankzeile** — sonst wäre jeder
anonyme Seitenaufruf ein Schreibvorgang und die Surface ein Amplifikationsziel.
Er kann nichts autorisieren, also gibt es auch nichts zu widerrufen.

### 8.2 Bindung des Cookies

Ein Cookie ist host-gebunden, eine Surface nicht notwendig. Der Umschlag trägt
deshalb Tenant, Workspace, Surface und Audience mit und wird gegen die aufgelöste
Surface geprüft; ein Cookie von Surface A ist auf Surface B wertlos, auch wenn beide
denselben Host teilen. Transport ist `HttpOnly`, `Secure` (wo HTTPS), `SameSite=Lax`
und `Path=/`. **Keine Ablage langlebiger Tokens in `localStorage`.**

### 8.3 Übergang Gast → angemeldet (Session-Fixation)

Beim Übergang wird der Kontext-Token **rotiert**; die Daten wandern mit, der Token
nicht. Sonst ist es klassische Session-Fixation: Angreifer setzt dem Opfer einen ihm
bekannten Token, Opfer meldet sich an, Angreifer hat die Sitzung.

Weil dabei auch das Subjekt wechselt (`callora.surface-guest/<id>` →
`<issuer>/<subject>`), veröffentlicht der Host ein Promotion-Ereignis mit **altem und
neuem Subjekt**. Daran hängen Plugins ihre Migration — Warenkorb, Entwurf,
Fortschritt. Der Host selbst migriert nichts: er kennt die Daten nicht.

### 8.4 Cross-Origin-Handoff

Surfaces können verschiedene Hosts besitzen; ein an einen Origin gebundenes Cookie
reicht nicht. Ein langlebiger universeller Bearer-Token zwischen allen Surface-Hosts
ist ausgeschlossen. Stattdessen:

```text
Quell-Surface
  → einmaliges Handoff-Ticket anfordern
  → Ziel-Surface aufrufen
  → Ticket gegen zielgebundene Surface-Session tauschen
```

Invarianten: kurze Gültigkeit (Default 60 s), Audience-Bindung an die konkrete
Ziel-Surface, Einmalverwendung, serverseitige Invalidierung, definierte Redirect-,
Origin- und CORS-Regeln.

Konkret: `POST /surface/handoff/tickets` auf der Quell-Surface,
`GET /surface/handoff/redeem` auf dem Ziel-Host. Gespeichert wird nur der
SHA-256-Hash des Geheimnisses, und die Einlösung löscht die Zeile und gibt zurück,
was sie gelöscht hat — Einmalverwendung ist damit eine Datenbank-Eigenschaft, keine
Prüfung, die zwei gleichzeitige Einlösungen umlaufen können. Eine abgelehnte
Präsentation verbraucht das Ticket trotzdem. Der Return-Pfad muss site-relativ sein;
alles andere wird auf `/` reduziert, sonst wäre die Einlöse-Route ein Open Redirect.
Das Ausstellen verlangt einen passenden `Origin`, denn ein Cross-Site-POST würde am
Cookie des Besuchers mitfahren.

### 8.5 Origin-Prüfung an cookie-getragenen Nahtstellen

Ein Browser hängt Cookies an einen WebSocket-Handshake und an einen Cross-Site-POST,
und keine Same-Origin-Policy hält ihn davon ab. Wo der Host das Surface-Cookie als
Credential akzeptiert (WebSocket-Upgrade, Handoff-Ausstellung), prüft er deshalb den
`Origin`-Header gegen den angefragten Host. Ein fremder Origin bekommt keinen Caller
beziehungsweise wird abgelehnt. Fehlt `Origin` ganz, ist es kein Browser — ein Client,
der das Cookie ohne Origin schickt, hat das bewusst getan.

---

## 9. Transport in die Konsumenten

| Konsument | Form |
|---|---|
| SSR-Template | `SurfaceRenderContext` erhält den Caller als allowlistete Skalare (ADR-015 §8) — keine .NET-Typen, keine Cookie-Werte, kein Token |
| Browser | `data-*` am Wurzelelement → `SurfaceContext.caller` im Surface-SDK, als diskriminierte Union |
| Surface-API | jede Anfrage trägt den normalisierten Caller (Baustein B) |
| WebSocket | der Connect-Gate akzeptiert die Surface-Session als Credential |

Der Render-Kontext trägt **nie** das Session-Token. Ein Template kann den Caller
lesen, aber nicht seine Sitzung weiterreichen.

---

## 10. Konsequenzen

**Positiv**

* Der Core bleibt domänen-neutral: kein Kunden-, Patienten- oder Mitgliedermodell.
* Wiedererkannte Gäste sind ein erstklassiger Fall, kein Nachrüsten.
* Gast/Authentifiziert lässt sich nicht versehentlich verwechseln.
* Interne und externe Nutzer derselben Surface funktionieren nebeneinander.
* Die Nahtstelle ist ab Tag eins durch VideoConference erprobt.

**Kosten**

* `WorkspaceSurface` erhält vier Spalten und eine Migration.
* Ein Anbieterwechsel meldet alle Nutzer der Surface ab. Das ist beabsichtigt.
* Plugins, die Gastzustand führen, **müssen** das Promotion-Ereignis behandeln;
  wer es ignoriert, verliert beim Login den Warenkorb.
* Der Host trägt eine zweite Session-Mechanik neben der Backend-Session. Beide
  bleiben getrennt: eine Surface-Session ist nie eine Backend-Session.

**Risiken**

* Ein Anbieter-Plugin, das langsam antwortet, verzögert jedes Rendern der Surface.
  Deshalb die harte Frist und ein sichtbarer Anbieterfehler statt stiller Degradation.
* Die Frist macht Identität von der Erreichbarkeit des Plugins abhängig. Für
  `Authenticated` ist Schließen die richtige Antwort, für `Mixed` das anonyme
  Weiterlaufen bei abgelehnten geschützten Routen.

---

## 11. Nicht-Ziele

* Kunden-, Patienten-, Agenten- oder Mitgliedertabellen im Core.
* Login-UI, Passwort-Reset oder fachliche Benutzerverwaltung.
* Plattformrollen für Surface-Nutzer.
* Fachliche Autorisierung: der Host transportiert Claims und interpretiert keine.
* Ein Ersatz für die Backend-/Admin-Authentifizierung.

---

## 12. Lieferschnitt

| Slice | Inhalt |
|---|---|
| **A1** | Anbieter-Vertrag, Caller-Modell, Normalisierung, Capability `surface.identity` |
| **A2** | Bindung auf `WorkspaceSurface`, Auflösung mit Verfügbarkeits-Gate, Session-Mechanik, Promotion |
| **A3** | Transport in `SurfaceRenderContext`, SSR-Shell und Surface-SDK |
| **A4** | WebSocket-Connect-Autorisierung über die Surface-Session |
| **A5** | Cross-Origin-Handoff-Tickets |

A1–A3 und A4–A5 werden getrennt geliefert.
