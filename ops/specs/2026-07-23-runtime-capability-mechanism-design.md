# Runtime-Capability-Mechanismus — Design

**Datum:** 2026-07-23
**Status:** Design abgestimmt (Understanding Lock + alle Abschnitte bestätigt)
**Kontext:** Anschluss an B4-deep (Communication-Voice). Der ursprünglich als „Capability-Flip
`foundation`→`voice`" gedachte Schritt trifft im statischen Capability-System keine dynamische
Fläche; stattdessen ein generischer, health-abgeleiteter Runtime-Capability-Mechanismus.

---

## 1. Understanding Summary

Ein **generischer Host-Mechanismus für Runtime-Capabilities**: Ein Plugin kann eine Capability
(z.B. `communication.voice`) nur dann als *bereitgestellt* gelten lassen, wenn zur Laufzeit eine
gesunde Fläche existiert (z.B. ≥1 gesunder Voice-Channel in der Registry). Der Host leitet die
effektive Provided-Set aus **statisch (Manifest) ∪ runtime-erfüllt** ab und reagiert **fail-closed**
auf Wegfall.

- **Warum:** Ehrliche Capability-Semantik — ein Consumer (Dialer/AiAgent) soll nicht gegen Voice
  aktiv sein oder bleiben, das kein Runtime bedienen kann.
- **Für wen:** Erster Consumer ist der Voice-Fall; der Mechanismus ist bewusst generisch für
  künftige health-abhängige Capabilities.
- **Kernverhalten:** reaktiv/fail-closed mit **Grace-Period** (Default 30s, global konfigurierbar)
  beim Wegfall; **Rückkehr wirkt sofort**; **Auto-Resume** ohne Operator-Zutun.

## 2. Schlüssel-Erkenntnis (Architektur-Fundament)

`PluginAvailability.IsAvailable` (inkl. Faktor `RequiredCapabilitiesAvailable`) wird **bereits zur
Laufzeit als Gate konsumiert**: `PluginApiEndpointDataSource` (HTTP-Routen) und
`WorkspaceUiChainResolver` (UI-Extensions) blenden ein Plugin bei Nicht-Verfügbarkeit aus.
`WorkspacePluginActivation` trägt nur `IsActive` (kein Reason-/Suspend-Zustand).

Daraus folgt: **„Suspend" muss nicht neu gebaut werden** — es *ist* `EffectivelyAvailable=false` über
das bestehende Gate. Der Mechanismus speist Runtime-Capabilities in genau diese Ableitung ein:

- Runtime-Capability fällt weg → `RequiredCapabilitiesAvailable=false` → `EffectivelyAvailable=false`
  → Routen/UI-Gates blenden die Runtime-Fläche des Dependents aus = fail-closed Suspend.
- Rückkehr → `EffectivelyAvailable=true` → Fläche wieder da = Auto-Resume ohne Zutun.
- **Kein `IsActive`-Mutieren, kein neuer persistierter Runtime-/Suspend-Zustand, keine orchestrierte
  Deactivate-Kaskade** — die Kaskade ist *implizit*, weil jede Availability-Evaluation die
  geforderten Capabilities gegen verfügbare Provider prüft. (Das statische Manifest-Feld
  `conditionalCapabilities` aus §3.1 ist Install-Metadaten, kein Runtime-Zustand.)

## 3. Komponenten & Verträge

### 3.1 Manifest (statisch, beim Install persistiert)

- Neues Feld `conditionalCapabilities: string[]` in `registry.json`, getrennt von `capabilities`.
- `PluginInstallation` bekommt `GetConditionalCapabilities()` (analog Provided/Required), persistiert
  wie die übrigen Capability-Listen (Migration).
- Statische `capabilities` bleiben *immer* bereitgestellt.

### 3.2 Plugin-seitiger Vertrag (Export)

```csharp
public interface IRuntimeCapabilitySource
{
    // Aktuell erfüllte bedingte Capabilities je Scope (WorkspaceKey; null = global).
    IReadOnlyCollection<RuntimeCapabilityGrant> CurrentGrants { get; }

    event Action<RuntimeCapabilityChanged>? CapabilitiesChanged;
}

public sealed record RuntimeCapabilityGrant(string Capability, string? WorkspaceKey);
public sealed record RuntimeCapabilityChanged(string Capability, string? WorkspaceKey, bool Satisfied);
```

Symmetrisch zu den anderen Contributors (`context.Export<IRuntimeCapabilitySource>(...)`). Reine
Capability-Codes + Scope — kein SDK-/Domänen-Leak.

### 3.3 Host-seitig

- **`RuntimeCapabilityRegistry`** (Singleton): abonniert beim Plugin-Start jeden exportierten
  `IRuntimeCapabilitySource`, hält den **effektiven** Grant-Zustand je `(pluginId, capability, scope)`,
  wendet den Grace-Timer an, stellt `IsSatisfied(pluginId, capability, workspaceKey)` bereit und feuert
  beim effektiven Flip ein Change-Signal. Meldet ein Source-Handler eine Exception, wird geloggt und die
  Capability als **unerfüllt** behandelt (fail-closed).
- **`PluginCapabilityGuard`** konsultiert die Registry: **effektive Provided-Set eines Plugins =
  statisch-Provided ∪ {bedingte Capabilities, die die Registry für diesen Scope als erfüllt meldet}**.

## 4. Datenfluss (Wegfall, Voice-Beispiel)

```
Voice-Channel Health → Down (in Workspace W)
  → CommunicationRuntimeCapabilitySource.CapabilitiesChanged(voice, W, false)
  → RuntimeCapabilityRegistry startet Grace-Timer für (communication, voice, W)
  → nach Ablauf noch unerfüllt → effektiver Flip auf unerfüllt + Change-Signal
  → availability-abgeleitete Gates invalidieren (Endpoint-Change-Token / UI-Chain-Cache)
  → Dependents in W, die communication.voice fordern: RequiredCapabilitiesAvailable=false
  → PluginApiEndpointDataSource / WorkspaceUiChainResolver blenden deren Fläche aus
```

Rückkehr (`Satisfied(voice, W, true)`): sofortiger effektiver Flip, laufender Grace-Timer abgebrochen,
Change-Signal → Gates re-evaluieren → Dependents wieder verfügbar (Auto-Resume).

## 5. Grace-Timer-Semantik (monotone Clock / `TimeProvider`)

- **`Satisfied`** gemeldet → sofort effektiv erfüllt; ein evtl. laufender Grace-Timer für dieses
  `(plugin, cap, scope)` wird abgebrochen. Rückkehr wirkt immer sofort.
- **`Unsatisfied`** gemeldet, aktuell effektiv erfüllt → Grace-Timer starten (Default 30s, global via
  Host-Setting konfigurierbar). Läuft er ab und ist immer noch unerfüllt → effektiver Flip auf
  unerfüllt + Change-Signal. Kommt vor Ablauf `Satisfied` → Timer weg, kein Flip.
- **Idempotenz:** mehrfaches `Unsatisfied` verlängert den laufenden Timer nicht neu (erste Meldung
  zählt).

## 6. Change-Signal → Gate-Invalidierung

Der effektive Flip triggert eine Neubewertung der availability-abgeleiteten Gates.
`PluginApiEndpointDataSource` und `WorkspaceUiChainResolver` cachen — beide erhalten ein
Invalidierungs-/Change-Token, das die Registry beim Flip feuert; so greift der Flip ohne Neustart.

**OQ (im Plan final zu verifizieren):** Ob `PluginApiEndpointDataSource` per-Request pullt oder ein
Change-Token braucht — Fallback ist das Change-Token. Diese Verifikation ist Teil von Slice S3.

## 7. Transitivität (implizit, korrekt)

`PluginCapabilityGuard.HasActiveProvider` zählt heute einen Provider bei `IsActive`. Neu: es zählt ihn
nur, wenn seine **effektive Provided-Set** (statisch ∪ runtime-erfüllt) die Capability enthält. Folge:
verliert Provider A seine eigene bedingte Capability, zählt A nicht mehr als Provider → Dependent B (der
A's Capability fordert) verliert `RequiredCapabilitiesAvailable` → B's Fläche gatet aus. Kaskade über
beliebige Tiefe, ohne aktive Orchestrierung, weil jede Availability-Evaluation die Kette neu prüft.

## 8. Scope

Grants sind `(capability, workspaceKey?)`. Ein globaler Consumer (`workspaceKey=null`) wird gegen
globale Grants geprüft; ein Workspace-Consumer gegen Workspace-Grants (Voice-Fall). Die Registry hält
beide Ebenen getrennt.

**Scope-Matching im Guard (eindeutig):** Prüft der Guard, ob Plugin A die Capability C für einen
Consumer in Workspace W bereitstellt, gilt A als Provider, wenn A einen effektiven Grant für C im
Scope W **oder** einen globalen Grant für C hält (global deckt alle Workspaces ab; ein Workspace-Grant
gilt nur in seinem Workspace). Statisch-Provided (aus `capabilities`) ist immer scope-übergreifend.

## 9. Erster Consumer: Communication-Voice

- `registry.json`: `conditionalCapabilities: ["communication.voice"]` (statisch bleibt
  `["communication.foundation"]`).
- Neuer Export `CommunicationRuntimeCapabilitySource : IRuntimeCapabilitySource`: meldet `voice` in
  Workspace W erfüllt gdw. ∃ registrierter Channel in W mit `communication.voice` **und** `Health==Up`.
- Getrieben von **neuen Channel-Health-Change-Events**: `Health` ist heute pull-only;
  `ICommunicationChannelRegistry` bzw. `SdkVoiceChannel` bekommen ein `HealthChanged`-Event
  (LineState-Transition → Health-Neubewertung → Registry-Aggregation → `CapabilitiesChanged`).
- Damit wird Voice **runtime-ehrlich** advertised statt statisch.

## 10. Testing

- **Core, unit:** Grace-Timer (erfüllt→unerfüllt→Ablauf→Flip; Rückkehr-vor-Ablauf→kein Flip;
  sofort-Resume) mit injizierter Test-Clock (`TimeProvider`); Guard-Integration (effektive
  Provided-Set, Transitivität); Change-Signal feuert bei Flip.
- **Core, integration:** Fake-Plugin mit bedingter Capability + Fake-Dependent → `EffectivelyAvailable`
  des Dependents kippt mit Grace; Provider-Rückkehr → Auto-Resume.
- **Voice-Consumer:** Fake-Channels flippen Health → Source meldet korrekt; End-to-End über den echten
  Registry/Health-Pfad.
- Keine Wall-Clock-Sleeps in Tests — Grace-Timer gegen `TimeProvider`/Test-Clock.

## 11. Zerlegung (je eigener Branch → PR, fake-testbar)

- **S1** — Manifest-Feld `conditionalCapabilities` + Persistenz
  (`PluginInstallation.GetConditionalCapabilities`, Recorder, Migration).
- **S2** — `IRuntimeCapabilitySource`-Vertrag + `RuntimeCapabilityRegistry` (Grace-Timer, Snapshot,
  Change-Signal) — reines Core, unit-getestet.
- **S3** — `PluginCapabilityGuard`/`PluginAvailabilityEvaluator`-Integration (effektive Provided-Set +
  Transitivität) + Gate-Invalidierung beim Flip (inkl. Verifikation `PluginApiEndpointDataSource`).
- **S4** — Communication-Voice-Consumer (Channel-`HealthChanged`-Events +
  `CommunicationRuntimeCapabilitySource` + Manifest-Deklaration).
- **S5 (optional)** — Observability: Admin-Sicht „suspended wegen fehlender Capability C"
  (UnmetFactor + Capability sichtbar).

## 12. Nicht-Ziele

- Kein `IsActive`-Mutieren; keine orchestrierte Deactivate-Kaskade.
- Keine Änderung an Entitlement-/Tenant-/Workspace-Aktivierungs-Faktoren.
- Keine Grace-Konfiguration pro Capability (nur global).
- Kein ALC-Unload bei Suspend.
- Kein Ersatz des statischen Capability-Systems (statische `capabilities` bleiben).

## 13. Decision Log

| Entscheidung | Alternativen | Begründung |
|---|---|---|
| Generischer Host-Mechanismus | Voice-spezifisch; generisch-minimal | Wiederverwendbar für künftige health-abhängige Capabilities; Voice ist erster Consumer. |
| Reaktiv fail-closed | Nur Availability spiegeln; nur Aktivierungs-Zeitpunkt | Dependent soll gegen fehlendes Voice-Runtime nicht aktiv bleiben. |
| Grace-Period (Default 30s) | Sofort; nur harte Zustände | Verhindert Kaskaden bei transienten SIP-Reconnects. |
| Auto-Resume via Availability-Gate | Hart deaktivieren+reaktivieren; manuell | Operator-Intent (`IsActive`) bleibt; selbstheilend; nutzt bestehendes `EffectivelyAvailable`-Gate. |
| Suspend = `EffectivelyAvailable=false` (kein neuer State) | Neuer Suspended-Zustand; Workspace-Activation-Overlay mit Reason | Gate existiert bereits (`PluginApiEndpointDataSource`/`WorkspaceUiChainResolver`); minimal-invasiv. |
| Plugin exportiert `IRuntimeCapabilitySource` (Snapshot + Event) | Host-Sink (push-only); Host-Polling (pull-only) | Symmetrisch zum bestehenden Export-Modell; push für Reaktivität, pull-Snapshot für Evaluation. |
| Transitivität implizit über effektive Provided-Set | Aktive transitive Cascade-Orchestrierung | Jede Availability-Evaluation prüft die Kette neu; keine riskante Orchestrierung. |
