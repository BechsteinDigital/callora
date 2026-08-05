# Callora — Geschäftsmodell & strategisches Zielbild

Datum: 2026-07-18 · Status: Zielbild bestätigt (Decision-Log) · Quelle: Brainstorming-Sitzung

## Understanding Summary

- **Was:** Callora ist eine erweiterbare **.NET-Plattform für Real-Time-Communication**. Das
  *kommerzielle Produkt* sind eigene **System-Plugins** (Communication/Voice-AI/Dialer/…), nicht die
  Plattform selbst. Die Plattform ist Fundament + Adoptionsmotor (Odoo-Modell).
- **Warum es einen weißen Fleck gibt:** Real-Time-Communication (SIP, Media, langlebige/nebenläufige
  stateful Prozesse) ist in **PHP architektonisch unmöglich** (shared-nothing, Request-Response). Das
  dominante Plattform-Ökosystem-Modell (Shopware/WordPress/Symfony) hängt an PHP → kann Communication
  nicht als Plugin-Ökosystem bedienen. .NET kann es. Damit existiert die Kategorie „erweiterbare,
  self-hostable, .NET-native Communication-Plattform" schlicht nicht.
- **Wettbewerb & Positionierung:** Twilio/Genesys/Amazon Connect = Real-Time, aber *Cloud/proprietär/US,
  nicht self-hostable, nicht als Plugin-Plattform erweiterbar*. Asterisk/FreeSWITCH = self-hostable, aber
  *Low-Level-Engines*, keine Business-Plattform mit Ökosystem/DX. **Weißer Fleck = erweiterbar +
  self-hostable/EU + .NET + Real-Time.** (Callora kann Asterisk/FreeSWITCH sogar als Media-Engine nutzen
  und die Business-/Orchestrierungsschicht darüber sein.)
- **Für wen:** Zielkunden zunächst über die eigenen System-Plugins. Kaufgrund ist die *Kombination*
  (self-hostable UND anpassbar UND Real-Time) — dort, wo self-hosted/EU nicht Preisargument, sondern
  rechtlich/architektonisch **kaufentscheidend** ist.

## Decision-Log

1. **Single-Tenant pro Instanz.** Reseller/In-App-Multi-Tenancy = bewusst YAGNI. Harte Kundengrenze =
   Infra/Instanzgrenze, nicht In-App-Query-Filter. (Markt-Muster: Plattform-Anbieter lösen „viele
   Endkunden" über viele Instanzen + Control-Plane, nicht über In-App-Multi-Tenancy.)
2. **Cloud v1 = managed hosting** („schnell starten") als Onboarding-/Community-Motor. Die **Control-Plane**
   (Provisioning/Billing/Instanz-Lifecycle) ist ein *separates System neben* Callora, kein In-App-Umbau.
   Data-Plane (die Kundeninstanz) bleibt die heutige `platform`/`workspace`-Architektur.
3. **Geschäftsmodell = Open-Core à la Odoo.** Plattform offen; Umsatz aus eigenen kommerziellen
   System-Plugins (+ Hosting). Ein Drittanbieter-Ökosystem ist späterer Bonus, **kein** Muss → entlastet
   von Community-Building-Last.
4. **Lizenz-Architektur:** Core **AGPL** (Netzwerk-Copyleft schützt gegen Cloud-Trittbrettfahrer + liefert
   den Dual-License-Hebel), SDK **Apache 2.0** (permissiv für Adoption; expliziter Patent-Grant — relevant
   im patent-sensiblen Voice/Codec-Feld). **Nicht MIT.** Abhängigkeitsrichtung: SDK ist untere Schicht,
   Plattform baut darauf (Apache→AGPL kompatibel, nicht umgekehrt).
5. **Erweiterbarkeit + Contract-Disziplin von Anfang an = bewusst richtig.** Saubere Grenzen
   (Public-vs-Internal, definierte Extension-Points) lassen sich *nicht* nachträglich einziehen, ohne die
   Plattform aufzureißen — früh gelegt, hält das die **Dritt-Plugin-/Ökosystem-Option offen** (Optionswert)
   und ist auch für die eigenen Plugins die gesündere Bauweise. Davon zu unterscheiden ist die *Intensität*
   des laufenden Governance-Apparats (BC-/API-Compat-Gates, Baselines): die folgt idealerweise dem realen
   Consumer-Stand — voll vergolden erst, wenn externe Consumer existieren, die man nicht brechen darf. Die
   Disziplin bleibt früh; der volle Apparat wird kalibriert. Markt-Beweis am ersten Plugin geht dem weiteren
   Ausbau dennoch voraus (Voice-Kern = Rohstoff → SDK/Produkt).
6. **Beachhead-Kandidat: Voice-AI-Agents.** Junges, unkonsolidiertes Segment mit Timing-Rückenwind
   (der „E-Commerce-Boom-Moment"). Wedge-Hypothesen: Gesundheitswesen / öffentlicher Sektor DACH, wo
   Datensouveränität den US-Cloud-Weg (Twilio/OpenAI-Realtime) rechtlich ausschließt.

## Offene Punkte (nächste Validierung)

- **Wedge/erster konkreter Kunde ist offen** — der eine Faktor, der über alles entscheidet. Nächster
  ehrlicher Schritt ist *kein Code*: Gespräche mit potenziellen Kunden im gewählten Wedge.
- **Welches System-Plugin ist als erstes marktreif — und für wen?** (Der eigentliche Markt-Test wandert
  von der Plattform auf die Plugins.)
- **CLA/Rechteinhaberschaft** vor dem ersten externen Beitrag klären (sonst kein Dual-Licensing möglich).
- **Lizenz-Umstellung** (heute MIT-Intention → AGPL-Core + Apache-SDK) als konkreter Task; finale
  Dual-License-Mechanik von einem IP-Anwalt prüfen lassen.

## Konsequenz für die laufende Plattform-Arbeit

- **#3 (Operator-Permissions):** Least-Privilege-Härtung bestätigt — passt zur Single-Tenant-Data-Plane
  (platform-scope = Reichweite innerhalb der Instanz, Rechte aus RBAC; nur SuperAdmin Voll-Bypass).
- **Contract-Disziplin/Erweiterbarkeit bleibt** (Fundament fürs spätere Ökosystem, nicht nachrüstbar);
  die *Intensität* des Compat-Apparats am realen Consumer-Stand kalibrieren (Decision 5).
- Callora-Weiterbau ist gerechtfertigt als **Fundament für die eigenen kommerziellen Plugins** — und hält
  zugleich die **Dritt-Plugin-Ökosystem-Option** offen (bewusster Optionswert, nicht Umsatz-Muss, aber
  echter Zukunftshebel).
