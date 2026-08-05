# Callora Plugin-Plattform & kuratierter Marketplace — Gesamt-Spec

Stand: 19. Juli 2026
Status: Design-Grundlage (vollständig, inkl. bewusst wartender Teile). Synthese aus der
Chat-Architekturberatung + `callora-store/docs/CALLORA_PLUGIN_MARKETPLACE_PLAN.md`, verankert am
tatsächlichen Callora-Code-IST.

Zweck dieses Dokuments: das *Gesamtvorhaben* einmal vollständig festschreiben — was jetzt gebaut wird
und was bewusst wartet — damit ein späterer Start ohne Neuerkundung möglich ist. Es ersetzt nicht die
Einzel-Bausteine-Specs; es ist die Klammer darüber. Verwandte Specs:
`2026-07-19-plugin-signing-content-manifest-design.md` (Signing-Kette B1–B4, erledigt),
`2026-07-17-admin-shell-walking-skeleton-design.md`.

---

## 1. Kernentscheidung (settled)

Callora verfolgt bewusst das **Shopware-/Symfony-Modell**: Plugins dürfen fachliche Core-Funktionen
ergänzen, dekorieren und ausdrücklich freigegebene Implementierungen vollständig ersetzen, eigene API,
Jobs, Events, Datenmodelle, Migrationen, Admin-/Workspace-UI und Templates mitbringen.

Das Sicherheitsmodell ist **„Trusted in-process by Provenance"**. Zwei unabhängige Herleitungen —
die Architekturberatung im Chat und das Code-Review hinter dem Marketplace-Plan — landen auf demselben
Ergebnis; das ist das Vertrauenssignal, dass das Modell so tragfähig ist, wie es in .NET wird.

Die zentrale technische Wahrheit:

> Ein .NET-Plugin, das im Callora-Prozess beliebigen Core-Code ersetzen darf, besitzt faktisch die
> Rechte des Callora-Prozesses. `AssemblyLoadContext`, `internal` und kuratierte DI sind **keine
> Sandbox** gegen bösartigen Code.

Daraus folgt: Sicherheit entsteht auf **zwei Ebenen** — (1) vertragliche Core-Grenzen (wohlerzogene
Plugins nutzen nur deklarierte Extension Points; geschützte Invarianten sind dort nicht ersetzbar) und
(2) Vertrauen in den *ausgeführten* Code (identifizierter Publisher, vollständige Paketsignatur,
automatisierte Prüfung, manuelles Review, Attestierung, Operator-Consent, Revocation). Kein anonymer,
ungeprüfter Public-Marketplace in der ersten Ausbaustufe.

**Warum .NET/in-process überhaupt richtig ist** (Ergänzung aus der Beratung, im Review implizit): Der
eigentliche Unterschied zu PHP/Shopware ist nicht „mehr Systemzugriff" — PHP hat vollen Zugriff. Er ist
**shared-fate im langlebigen Prozess**: der Blast-Radius eines Plugins ist plattformweit, nicht
per-Request. Genau deshalb ist .NET für Callora trotzdem richtig — Voice/Communication braucht einen
zustandsbehafteten, langlebigen Prozess, für den PHPs Per-Request-Modell ungeeignet ist. Der Preis
dafür (shared-fate) wird durch Provenance-Trust + Resilienz-Härtung bezahlt, nicht durch eine
vorgetäuschte Sandbox.

---

## 1.1 Zwei Stufen — Pflicht (Stufe 1) vs. Option (Stufe 2)

Dieses Dokument beschreibt in voller Tiefe **Stufe 2**. Für das aktuelle Geschäftsziel ist zuerst
**Stufe 1** relevant. Die beiden Stufen sauber trennen, damit klar ist, was Pflicht und was Option ist:

**Stufe 1 — eigene Plugins als Produkte (Ein-Verkäufer, kein Marketplace).** Callora vermarktet seine
*eigenen* Plugins (System/Foundation-Tier, ein einziger Publisher = wir selbst). Es gibt **kein**
Publisher-Onboarding, keine KYC/KYB, keinen Provisions-Split/Payout, kein Review fremden Codes, keine
Marketplace-Attestierung, kein Community-/Sandbox-Tier. Gebraucht wird nur:
- **C1 Extension-Fläche** — ohne erweiterbaren Core gibt es kein verkaufbares Plugin. Der Produkt-Hebel.
- **schlanker Entitlement/Lizenz-Gate** — wer gekauft hat, darf aktivieren. Callora hat Entitlements
  bereits (getrennt von Aktivierung, fließt in Availability).
- **einfacher Ein-Verkäufer-Commerce** — SaaS-Checkout/Abo, ein Verkäufer, keine Connected Accounts,
  kein Payout. Bruchteil von M3; anfangs sogar über Vertrieb/manuell abbildbar.

Paket-Signatur (B1–B4 + C3-a) ist bereits da = Integrität in Prod, aber ein Hygiene-Faktor, kein
Verkaufs-Enabler.

**Stufe 2 — kuratierter Mehranbieter-Marketplace (dieses Dokument, §4–§13).** Dritt-Publisher
verkaufen fremden Code. Erst *hier* entstehen Publisher-Onboarding, Payout-Split (Mangopay),
Review-Pipeline, Attestierung, Trust-Tiers, Sandbox-Lane und das Store-Backend M0–M7. Optionswert, an
einen realen zahlenden Dritt-Publisher gekoppelt — bewusst vollständig vordokumentiert, aber nicht
committet.

| Baustein | Stufe 1 | Stufe 2 |
|---|---|---|
| Paket-Signatur (B1–B4, C3-a) | vorhanden (Integrität) | Pflicht |
| C1 Extension-Fläche | **Pflicht** (Produkt-Hebel) | Pflicht |
| Entitlement/Lizenz | schlanker Ein-Verkäufer-Gate | Store-Protokoll (C5) |
| Commerce | Ein-Verkäufer-Checkout | Marketplace-Split (M3/M4) |
| Publisher-Onboarding, KYC, Payout-Split | — | Pflicht (M1/M4) |
| Review fremden Codes, Attestierung, SBOM | — | Pflicht (M2, C3-Rest) |
| Community-/Sandbox-Tier | — | optional (§5.1) |
| Store-Backend M0–M7 | — | Pflicht |

**Konsequenz:** Der aktuelle Fokus ist Stufe 1. Der nächste konkrete Schritt ist **C1**; alles
Marketplace-/Payout-/Publisher-seitige bleibt §10.2 „wartet", bis Stufe 2 committed wird. C3-b
(Gesamtpaket-Hash) und die übrigen C3-Reste sind für Stufe 1 nicht erforderlich.

---

## 2. Understanding & Scope

**Was gebaut wird:** eine breit erweiterbare, kuratierte In-Process-Plugin-Plattform (Callora) plus,
als späterer, entkoppelter Layer, ein Mehranbieter-Marketplace (`callora-store`) für kommerzielle
.NET-Erweiterungen.

**Für wen:** zunächst Calloras eigene System-Plugins (Communication) und wenige geprüfte
Partner/Design-Partner; später identifizierte Dritt-Publisher im kuratierten Marketplace.

**Explizite Nicht-Ziele:**
- Kein enger App-Store mit harmlosen Widgets.
- Kein anonymer/ungeprüfter Public-Marketplace in Stufe 1.
- Keine vorgetäuschte technische Sandbox für In-Process-Plugins (Marketing/Consent dürfen das nie
  behaupten).
- Kein selbst geführtes Sammelkonto/Wallet auf Callora-Seite (regulierter Geldfluss bleibt beim
  Zahlungsanbieter — siehe §7).

**Produkt-vor-Plattform (Sequenzierungs-Leitplanke):** Der Marketplace-Commerce-Layer ist ein
offengehaltener Optionswert, kein committetes Ziel. Er wird erst relevant, wenn ein *zahlender
Dritt-Publisher* existiert. Bis dahin: kuratiert, schmal, produktgetrieben. Diese Reihenfolge ist Teil
des Specs, nicht nur eine Meinung (siehe §10).

---

## 3. IST-Stand (code-verankert, 2026-07-19)

### 3.1 Belastbar vorhanden

- **Runtime-Host:** `RuntimePluginHost` lädt Plugins in eigenem, entladbarem `AssemblyLoadContext`;
  Install/Activate/Deactivate/Uninstall; Exports werden beim Deaktivieren entfernt. Fehlgeschlagenes
  ALC-Unload wird erkannt und als Fehler sichtbar gemacht (`H2`, Task #49).
- **Kuratierte DI-Fläche:** `CuratedPluginServiceProvider` begrenzt die injizierte Servicefläche;
  `IPluginDataStore` an die jeweilige Plugin-ID gebunden.
- **Katalog & Verfügbarkeit:** Capabilities, Dependencies, Aktivierungsreihenfolge, Runtime-State,
  zentrale `PluginAvailabilityEvaluator`-Entscheidung; Entitlements getrennt vom Aktivierungszustand
  und in die effektive Verfügbarkeit eingerechnet (`H4`, Task #51).
- **Routen-Governance:** Plugin-API-Routen erzwingen Auth, Permission, Workspace-Scope; reservierte
  Host-Routen und Plugin-zu-Plugin-Kollisionen werden abgelehnt (`H9`/`M6`, Tasks #52/#56/#36);
  globale ASP.NET-Fallback-Policy verlangt standardmäßig Authentifizierung.
- **Persistence-Backstops:** Workspace-Lese-/Schreib-Backstops im EF-Core-Kontext (`H3`, Task #50).
- **Plugin-wins-Auflösung** für Jobs und Flow Actions; `[HostProtected]` verhindert das Ersetzen
  kritischer Host-Handler — aktuell auf **5 Job-Handlern** angewandt (`FlowExecuteJobHandler`,
  `MarketplaceEntitlementSyncJobHandler`, `MailSendJobHandler`, `RetentionCleanupJobHandler`,
  `WebhookDeliveryJobHandler`).
- **Signatur-Kette (B1–B4, erledigt):** signiertes Content-Manifest `plugin.signature.json`
  (ECDSA-P256+SHA-256), Trusted-Signer über Public-Key-Fingerprint, Signer-/Content-Revocation,
  Signaturstatus im Admin (`GET /api/plugins/signature-report` + Badge), Re-Verifikation beim
  Rehydratisieren. Cross-platform (Linux-tauglich), ersetzt das gebrochene Authenticode-Gate.
- **Entitlement-Sync** vorhanden (`MarketplaceEntitlementSyncJobHandler`), fließt in Availability ein.
- **Decoration-Mechanismus** vorhanden: `IServiceDecorator<TService>`, `PluginServiceDecoration`,
  Per-Call-Proxy (`DynamicallyDecoratedMailSender`).
- **Export/Erasure/Purge-Contributor + Aggregator** (`H6`, Task #53).
- **Governance-Analyzer** (CAL0001/0002/0003, `[CalloraInternal]`, PublicApiAnalyzers-Baseline).

### 3.2 Entscheidend offen (gegen das Marketplace-Ziel)

Am Code bestätigte Lücken — nicht bloß aus dem Review übernommen:

1. **Extension-Fläche ist Mechanismus, nicht Fläche.** 25 `[CalloraExtensible]`-Marker, aber real
   dynamisch dekoriert ist **nur `IMailSender`** (`CalloraHostCompositionExtensions:204`). Plugin-wins
   zentral nur für Jobs + Flow Actions. Callora ist Shopware-*förmig*, nicht Shopware-*breit*.
2. **Signatur deckt nicht das ganze Paket.** `PluginSigner.SignAsync` hasht exakt
   `[registry.AssemblyFileName, "registry.json"]` (`PluginSigner.cs:59`). Abhängige DLLs, UI-Bundles,
   CSS, Templates, Migrationen sind **nicht** abgedeckt; der Verifier lehnt zusätzliche, nicht im
   Manifest gelistete Dateien **nicht** ab. → verletzt „kein ausführbarer Inhalt außerhalb der
   signierten Liste".
3. **Kein Marketplace-Installationskanal.** NuGet-Resolver nutzt lokalen Cache. Download,
   kurzlebige Store-Autorisierung, Staging, atomare Installation, Rollback, Anti-Downgrade fehlen.
4. **UI läuft mit vollem Vertrauen im Shell-Origin.** Admin-Bundles als klassische Scripts im
   Dokumentkontext (DOM-/Origin-/Session-Zugriff). Für geprüfte, vollsignierte Plugins vertretbar,
   aber CSP-/Supply-Chain-Härtung fehlt.
5. **Entitlement-Sync ist kein Store-Protokoll.** Nutzt allgemeine Callora-Auth/Permission; es fehlen
   dedizierte Marketplace-Identität, signierte Requests mit Zeitfenster, Inbox-Dedup, Reconciliation.
6. **Trust/Revocation sind lokale Konfiguration.** Publisher-Keys, Marketplace-Root, Key-Rotation,
   Revocation-Metadaten werden nicht automatisch + signiert aus dem Store bezogen.
7. **Plugin-Berechtigungen sind kein prüfbarer Vertrag.** Capabilities beschreiben Produktfähigkeit/
   Abhängigkeiten, nicht erwarteten Zugriff auf Netz, Dateisystem, native Libs, Prozesse, sensible
   Daten, UI-Scope.
8. **`callora-store` ist Demonstrator.** Katalog/Orders in-memory, keine Konten/Publisher, keine
   Release-Pipeline, kein Review, kein revisionssicheres Ledger, keine Marketplace-Auszahlung.

### 3.3 Reife-Einordnung (Planungsindikation, kein Messwert)

| Bereich | Reife fürs Ziel |
|---|---:|
| Plugin-Lifecycle & Runtime-Katalog | 75–85 % |
| Capabilities/Dependencies/Availability | 75–85 % |
| Breite Core-Decoration/-Replacement | 20–30 % |
| Paket-Signatur & Revocation | 55–65 % |
| Sichere Marketplace-Installation | 10–20 % |
| Trusted Plugin-UI | 45–55 % |
| Marketplace-Entitlements | 50–60 % |
| Store als Mehranbieter-Marketplace | 10–15 % |
| End-to-End-Marktplatz | 30–40 % |

Callora ist für dieses Ziel **nicht falsch aufgebaut** — das Modell ist bereits näher an Shopware als
an einem isolierten App-Store. Der größte Umbau ist kein Runtime-Austausch, sondern die konsequente
Fertigstellung dreier Dinge: breite klassifizierte Override-Fläche, vollständige Paket-/Provenance-/
Installationskette, belastbare Store-/Entitlement-/Revocation-Verbindung.

---

## 4. Ziel-Architektur — Callora-Seite

### 4.1 Vier Extension-Kategorien statt pauschalem „public"

Jede relevante Core-Fläche wird inventarisiert und genau einer Kategorie zugeordnet:

| Kategorie | Plugin-Recht | Beispiel |
|---|---|---|
| `Contributable` | weitere Implementierung hinzufügen | Event-Listener, Navigation, Exportabschnitt |
| `Decoratable` | bestehenden Service umhüllen, Verhalten ändern | Mail, Media, fachliche Policies |
| `Replaceable` | Implementierung vollständig ersetzen | nichtkritischer Resolver/Handler |
| `HostProtected` | nur reagieren/beitragen, finales Enforcement im Core | Auth, RBAC, Tenant-Scope, Trust, Entitlement |

`[CalloraExtensible]` wird um **Modus, Scope und Stabilitätsangabe** ergänzt (oder ein gleichwertiges
Metadatenmodell). Ein Analyzer prüft, dass ein Plugin nur deklarierte Extension Points verwendet. Ein
Host-Test stellt sicher, dass jeder geschützte Service auch in der *tatsächlichen Auflösung* geschützt
bleibt (nicht nur per Attribut).

### 4.2 Zwei technische Erweiterungswege

**A. Boot-time Overrides (tiefe Core-Eingriffe).** Plugins registrieren/dekorieren/ersetzen
freigegebene Host-Dienste beim DI-Container-Aufbau. Bildet Symfony-/Shopware-Verhalten in .NET sauber
ab. Gilt hostweit; Install/Update/Activate/Deactivate erfordern **Neustart oder Rolling-Restart**. Vor
dem Container-Build: deterministischer, validierter Extension-Plan; Konflikte, Zyklen und der Versuch,
`HostProtected` zu ersetzen, brechen fail-closed ab. Letzter funktionierender Plan bleibt für Rollback
verfügbar.

**B. Runtime Contributions (dynamische Funktionen).** Events, Pages, API-Controller, Jobs, Flow
Actions, Navigation, kontextuelle Decorators bleiben hot-pluggable über den vorhandenen
Runtime-Katalog. Auflösung kennt Plugin-ID + Workspace-Kontext; ein Export läuft nur, wenn das Plugin
im jeweiligen Workspace effektiv verfügbar ist. Deaktivierung entfernt Routen, Assets, Exports,
Hintergrundarbeit **vollständig**. Zeitlimits, Cancellation, Fehlerzähler, Circuit-Breaker verhindern,
dass ein defektes Plugin die Pipeline dauerhaft blockiert (→ shared-fate-Schutz, §4.6).

Diese Trennung löst den aktuellen Zielkonflikt: tiefe DI-Ersetzung wird zuverlässig/Shopware-nah,
während häufige Workspace-Aktivierung keinen Container-Neubau braucht.

### 4.3 Nicht überschreibbare Invarianten (`HostProtected`-Katalog)

Plugins dürfen Ereignisse empfangen und definierte Beiträge liefern, aber **nicht den finalen Ausgang**
kontrollieren von:

- Authentifizierung, Ausgabe/Validierung von Identitäten
- RBAC-/Permission-/Tenant-/Workspace-Enforcement
- Secret Store, Data Protection, Schlüsselmaterial
- Paketprüfung, Trust Store, Revocation, Plugin-Lifecycle-Autorität
- Marketplace-Entitlement, finale Availability-Entscheidung
- vollständiger Benutzerdatenexport, Erasure, Workspace-Purge
- unveränderliche Audit-Grundregeln

Wichtig: „geschützt" ≠ „nicht erweiterbar". Ein Plugin darf einen zusätzlichen Login-Provider, einen
Exportabschnitt oder einen Purge-Contributor liefern — der Core entscheidet abschließend, ob Auth,
Export oder Löschung vollständig/erfolgreich waren. Der heutige ad-hoc-`[HostProtected]`-Einsatz (5
Handler) wird zu einem **testbaren Core-Sicherheitskatalog** ausgeweitet.

### 4.4 Was „Core umschreiben" konkret heißt

Erlaubt: vollständiger Ersatz eines freigegebenen Servicevertrags; Decoration ohne Delegationspflicht;
Override eines nicht geschützten Handlers unter deterministischer Priorität; eigene API/Migrationen/
Jobs/Events/Shell-Slots/Pages/Templates; Erweiterung anderer Plugins über deren öffentliche Contracts.

Nicht Teil des Modells: Binärpatches an Callora-Assemblies; Austausch beliebiger Dateien im laufenden
Host; Reflection auf interne Sicherheitsimplementierungen als offizieller Vertrag; Überschreiben der
Trust-/Auth-/Scope-/Entitlement-Kette. Technisch *könnte* ein bösartiges In-Process-Plugin diese Regeln
umgehen — deshalb darf es erst nach Provenance- + Reviewentscheidung in den Prozess gelangen.

### 4.5 Supply-Chain / vollständige Content-Signatur

Aufbauend auf der erledigten B1–B4-Kette. Ziel-Zustand eines Release-Pakets (kanonisch, unveränderlich):

- `registry.json` (Plugin-ID, Version, kompatible Callora-Versionen, Entry Point, Trust-Tier);
- **alle** Managed- und Native-Assemblies;
- **alle** Admin-/Workspace-Bundles, Styles, Templates, statischen Assets;
- Migrationen und Contract-Assemblies;
- deklarierte Produkt-Capabilities, Dependencies, Berechtigungs-/Risikohinweise;
- SBOM und Build-Provenance;
- Hash jedes zulässigen Inhalts **und** ein Hash des Gesamtpakets;
- Publisher-Signatur **und** separate Marketplace-Attestierung.

Der Installer lehnt zusätzliche, fehlende oder veränderte Dateien ab; die Signaturdatei selbst ist die
einzige definierte Ausnahme vom Content-Set. Ein veröffentlichter Release wird **nie mutiert** —
Korrekturen erzeugen eine neue Version.

Delta zum IST (§3.2 Punkt 2): heutiges Manifest deckt nur DLL + registry.json; erweitern auf das ganze
Content-Set + „reject unlisted files"-Regel im Verifier. **Das ist der nächste konkrete Baustein**
(§10, Paket C3-a).

### 4.6 Resilienz gegen shared-fate (In-Process-Härtung)

Unabhängig vom Trust-Tier, weil es den langlebigen Prozess schützt: Zeitlimits + Cancellation für
Runtime-Contributions; Fehlerzähler + Circuit-Breaker pro Plugin/Export; vollständige Deregistrierung
von Routen/Assets/Exports/Hintergrundarbeit bei Deaktivierung; ALC-Unload-Watchdog (Leak-Erkennung ist
da, Eskalation/Sichtbarkeit ausbauen). Billige Versicherung gegen den plattformweiten Blast-Radius.

### 4.7 UI-Sicherheitsmodell

Marketplace-Verified-Plugins dürfen die Shell tief erweitern und laufen weiter im Shell-Origin —
Voraussetzungen: UI-Dateien Teil des vollsignierten Pakets; strikte CSP ohne unkontrolliertes `eval`/
fremde Origins; keine Inline-Scripts außerhalb der kontrollierten Bootstrap-Kette; Registrierung
synchron + kryptografisch der geladenen Plugin-ID zugeordnet; jedes UI-Asset bei Deaktivierung
vollständig deregistriert; Berechtigungs-/Workspace-Prüfung serverseitig (ausgeblendete UI ist **nie**
Autorisierung); Review prüft Datenabfluss, Credential-Zugriff, Dependency-Supply-Chain.

Die Loader-Basis (Micro-Frontend, `currentPluginId`-Attribution, Deregistrierung, Diagnose) existiert
bereits; offen sind CSP-Härtung + vollständige Signaturabdeckung der UI-Assets.

---

## 5. Trust-Tiers

| Tier | Herkunft | Installation |
|---|---|---|
| System/Foundation | BechsteinDigital, mit Distribution ausgeliefert | automatisch vertrauenswürdig, weiterhin signiert |
| Marketplace Verified | identifizierter Publisher, Review + Attestierung | Operator-Consent mit Änderungsübersicht |
| Community Signed | identifizierter Publisher, kein Vollreview | standardmäßig blockiert; später nur mit deutlicher Vollzugriff-Warnung |
| Unsigned/Unknown | keine belastbare Herkunft | Produktion: immer blockiert |

**Die erste kommerzielle Version akzeptiert nur System/Foundation und Marketplace Verified.**

### 5.1 Zwei-Tier-Trust — die ehrliche Korrektur

In der Beratung wurde Out-of-Process/WASM als Pfad „falls offenes Ökosystem" skizziert. Präzisierung
(das Review schärft das korrekt): Out-of-Process ist **kein Upgrade** des In-Process-Modells, sondern
ein **parallel schwächeres** Modell. Ein sandboxed-`iframe`-/Sidecar-Community-Plugin kann die
Host-UI und den fachlichen Core **per Definition nicht** tief umschreiben. Der Zwei-Tier-Split heißt
also:

- **In-Process (System/Foundation + Marketplace Verified): dauerhaft.** Volle Shopware-Freiheit,
  Sicherheit über Provenance + Review + Invarianten.
- **Sandbox-Lane (Community/ungeprüft): als geringere Fähigkeit, falls je.** Eigene Origin/`iframe` +
  Capability-Bridge; kann *nicht* gleichzeitig beliebig den Host umschreiben. Bewusste Produktgrenze.

Das ändert, was man Publishern verspricht: Community bekommt eine *echte, aber begrenzte* Lane, kein
„später auch alles". Ungeprüften Code, der trotzdem den ganzen fachlichen Core ersetzen soll, würde
eine vollständige Control-Plane/Data-Plane-Trennung erfordern (siehe Aufwand §9) — **für den
kuratierten Marketplace nicht empfohlen.**

---

## 6. Marketplace-Backend (`callora-store`) — Zielbild

### 6.1 Verantwortungstrennung

| System | Verantwortlichkeit |
|---|---|
| `callora-store` | Publisher, Katalog, Releases, Preise, Bestellungen, Provisionen, Ledger, Auszahlungen, Entitlements, Review |
| `callora` | Paketvertrauen, Installation, Update, Rollback, Aktivierung, Core-Extensibility, Workspace-Verfügbarkeit, Runtime-Gesundheit |
| Zahlungsanbieter (Mangopay) | Zahlungsabwicklung, Verkäufer-Verifikation (KYC/KYB), Wallets, regulierter Geldfluss, Auszahlung |
| Website | Marketing, öffentliche Plugin-Details, Einstieg in Login/Checkout |

### 6.2 Bounded Contexts

| Kontext | Kernobjekte |
|---|---|
| Identity & Access | Customer, Publisher, Membership, Role, ServiceIdentity |
| Publisher Compliance | VerificationProfile, Agreement, TaxProfile, PaymentAccount |
| Catalog | PluginProduct, Listing, Category, Media, Compatibility |
| Release Management | PluginRelease, Artifact, Signature, Review, ScanFinding, Publication |
| Commerce | Offer, Price, Subscription, Order, Payment, Refund, Chargeback |
| Entitlements | License, Entitlement, Grant, Revoke, Device/Instance Binding |
| Revenue Accounting | LedgerAccount, LedgerEntry, Commission, Reserve, Adjustment, Payout |
| Distribution | DownloadTicket, DeliveryAudit, Revocation, UpdateChannel |

PostgreSQL = Source of Truth. Objekt-Storage hält unveränderliche Artefakte. Asynchrones via
Outbox/Inbox + idempotente Consumer.

---

## 7. Geldfluss & Auszahlung — Mangopay (ENTSCHEIDUNG)

Paddle ist **verworfen**. Gewählt: **Mangopay**.

**Begründung:** Mangopay bietet Wallets, Guthabenhaltung, gesteuerte Auszahlungszeitpunkte und
KYC/KYB eingebaut; der regulierte Geldfluss liegt näher am Anbieter statt bei Callora. Das reduziert
Calloras Merchant-of-Record-/Steuer-/Negativsaldo-Exposure gegenüber dem Stripe-Connect-Plattform-
Modell — zum Preis eines umfangreicheren Payment-Services-Integrationsmodells (E-Wallet-System,
Payin/Payout-Flows, KYC-Dokument-UX). Quellen:
<https://docs.mangopay.com/guides/e-wallet-system>, <https://docs.mangopay.com/guides/payouts>.

Callora/`callora-store` bauen **keine eigene Wallet** und überweisen Publisherguthaben **nicht** aus
einem selbst geführten Sammelkonto. Das lokale Double-Entry-Ledger ist eine **revisionsfähige
Spiegelung** für Provision, Refunds, Chargebacks, Rücklagen, Abrechnung — **nicht** der rechtliche
Geldspeicher.

**Entscheidungsgate (fail-closed):** Kein produktiver Commerce-Code für Split/Payout, bevor
schriftlich geklärt ist: Mangopay-Vertrag/Partnerzugang, Merchant-of-Record-/Rechnungssteller-Frage,
Umsatzsteuer, Refund-/Chargeback-Haftung, Auszahlungsrhythmus. Bis dahin bleibt Commerce ein
Test-Geldfluss.

**Fallback dokumentiert:** Falls Mangopay-Onboarding/-Konditionen scheitern, ist **Stripe Connect** der
pragmatische Fallback und schnellste MVP-Weg (bestehender Stripe-Code reduziert Einstieg; dafür MoR-/
Steuer-Verantwortung bei Callora). Adyen for Platforms = spätere Scale-Option. Diese Rangfolge ist
bewusst festgehalten, damit ein Providerwechsel nur den Commerce-/Payout-Kontext trifft, nicht die
Architektur.

---

## 8. Store ↔ Callora — zwei getrennte Protokolle

**Artifact Protocol.** Callora authentifiziert sich als konkrete Installation/Tenant-Instanz. Store
stellt ein **kurzlebiges, einmalig verwendbares** Download-Ticket für genau einen Release aus; Ticket
enthält keine langfristigen Secrets und ersetzt **nie** die lokale Paket-Signaturprüfung. Download,
Hash, Installationsresultat werden beidseitig auditiert.

**Entitlement Protocol.** Dedizierte Marketplace-Service-Identity (statt allgemeiner Operator-API).
Signierte Nachricht mit Event-ID, Tenant, optionalem Workspace, Plugin, Aktion, Zeitpunkt,
Gültigkeitsfenster. Inbox-Dedup + Entitlement-Änderung in **einer lokalen Transaktion**. Periodische
Reconciliation zusätzlich zu Webhooks. Refund/Chargeback/Abo-Ende/manuelle Sperre haben definierte
Grace-/Revoke-Regeln. Bei Store-Ausfall: dokumentierte Fail-closed-/Grace-Policy — Bestandskunden
verlieren nicht bei kurzem Netzausfall sofort produktive Funktionen.

### 8.1 Sichere Installation/Aktualisierung (Zielablauf)

1. Store stellt tenant-/releasegebundenes Download-Ticket aus.
2. Callora lädt in **neu erzeugte Staging-Zone**, nie direkt ins aktive Plugin-Verzeichnis.
3. Paketgröße/Pfade/Dateitypen/Entpack-Limits werden vor + während des Entpackens geprüft.
4. Gesamt-/Einzelhash, Publisher-Signatur, Attestierung, Revocation, Kompatibilität, Dependencies,
   Berechtigungsänderungen validiert.
5. Analyzer-/Contract-Smoke-Tests + Migration-Plan laufen vor Aktivierung.
6. Operator bestätigt neue/erweiterte Rechte + hostweite Overrides.
7. Dateien + DB-Zustand atomar auf neue Version umschalten.
8. Health-Check + Beobachtungsphase entscheiden über Erfolg/Rollback.
9. Erst danach alten Release entfernen; Audit/Telemetrie enthalten Paket-/Provenance-Hashes.

Downgrades nur als expliziter, auditierter Rollback auf eine zuvor freigegebene Version.

### 8.2 Berechtigungsmanifest

Beschreibt mindestens: hostweite Service-Replacements/Decorators; sensible Datenklassen +
Workspace-/Tenant-Scope; ausgehende Netzwerkziele/-klassen; Dateisystem-/Native-Code-/Prozessbedarf;
Hintergrundjobs + Frequenz; Admin-/Workspace-UI + Permissions; Migrationen, Retention, Export-/Erasure-/
Purge-Contributors. Bei In-Process-Plugins ist das **Review-/Consent-/Auditmaterial** — es kann Zugriff
über Callora-eigene APIs einschränken, aber direkten .NET-Datei-/Netzzugriff **nicht** zuverlässig
sandboxen (ehrlich benennen).

### 8.3 Release-Pipeline (Store)

`Draft → Uploaded → AutomatedChecks → ManualReview → Approved → MarketplaceAttested → Published →
Deprecated|Revoked`. Automatische Gates: Paketformat + vollständige Hashabdeckung; Malware-/Secret-/
Dependency-Scan; SBOM + bekannte Schwachstellen; Callora-Analyzer + verbotene API-Nutzung; Contract-/
Kompatibilitätstests gegen unterstützte Callora-Versionen; Migration- + Install/Update/Uninstall-Smoke;
UI-CSP-/Assetprüfung; Lizenz-/Notice-Prüfung. Manuelles Review fokussiert Core-Replacements, sensible
Daten, Netzzugriff, native Libs, Migrationsverhalten, Widersprüche zum Berechtigungsmanifest.

---

## 9. Arbeitspakete & Aufwand

Developer-Weeks für erfahrene .NET-/Web-Entwickler inkl. Tests + technischer Doku, **ohne** Wartezeit
auf Providerfreigabe, Rechts-/Steuerberatung, Texte, Support-Betrieb.

### 9.1 Callora-Anpassungen

| Paket | Ergebnis | Aufwand | IST-Abgleich |
|---|---|---:|---|
| C0 – Threat Model & ADR | Schutzgrenzen, Override-Semantik, Trust-Tiers, Restart-Semantik | 1–2 | teils in ADR-014 |
| C1 – Extension-Point-Inventar | Core-Services klassifizieren, Marker/Analyzer, Protected Catalog | 2–4 | Marker + `IServiceDecorator` da, Fläche fehlt |
| C2 – Breite Override-Engine | boot-time Replace/Decorate, deterministische Pläne, Runtime-Dispatch, Workspace-Gating | 4–7 | Per-Call-Proxy da (1 Service) |
| C3 – Vollständige Supply Chain | Komplettpaket-Hashes, Publisher-+Store-Attestierung, Key-Rotation, Revocation | 2–4 | B1–B4 done; Full-Content + reject-unlisted offen |
| C4 – Remote Lifecycle | Store-Client, Staging, atomare Installation, Update, Health, Rollback | 3–5 | lokaler Install da, Remote fehlt |
| C5 – Entitlement-Protokoll | Service-Identity, Request-Signatur, Transaktion/Inbox, Reconciliation, Grace | 2–4 | Sync da, Protokoll fehlt |
| C6 – Trusted UI Hardening | vollständige Signatur, CSP, Attribution, Deregistrierung, Review-Gates | 2–4 | Loader/Attribution da, CSP fehlt |
| C7 – SDK & Kompatibilität | NuGet-SDK, Templates, Referenzplugins, API-Baseline, Contract-Matrix | 3–5 | Contract-Kit da |
| C8 – Betrieb & Incident Response | Telemetrie, Audit, Kill-Switch, Rollout, Backup/Recovery-Runbooks | 2–3 | Revocation-Kill-Switch da |

### 9.2 Marketplace-Anpassungen (`callora-store`)

| Paket | Ergebnis | Aufwand |
|---|---|---:|
| M0 – Plattformbasis | DDD-Struktur, PostgreSQL, Migrationen, Auth, Rollen, Outbox/Inbox | 3–5 |
| M1 – Publisher & Compliance | Onboarding, Organisationen, Agreements, **Mangopay**-Konto, Status | 3–5 |
| M2 – Catalog & Release | Produkte, Versionen, Kompatibilität, Artefakte, Reviewworkflow | 4–7 |
| M3 – Commerce | Preise, Checkout, Subscriptions, Orders, Refunds, Chargebacks | 3–5 |
| M4 – Provision & Payout | **Mangopay**-Wallets/Payout, Double-Entry-Ledger, Reserve, Statements, Reconciliation | 4–7 |
| M5 – Entitlements & Distribution | Grants/Revoke, Download-Tickets, Update-Kanäle, Callora-Sync | 3–5 |
| M6 – Portale | Publisher-, Kunden-, Admin-Portal, öffentliche Storeseiten | 4–7 |
| M7 – Produktionshärtung | Security, Observability, Backups, Rate Limits, Datenschutz, E2E | 3–5 |

### 9.3 Gesamtaufwand

| Ziel | Gesamt | Kalenderzeit |
|---|---:|---:|
| End-to-End Design-Partner-Beta | 28–42 DW | 4–6 Monate, 2 Entwickler |
| Produktiver kuratierter Marketplace | 46–68 DW | 6–9 Monate, 2–3 Entwickler |
| Offener/ungeprüfter Markt (eingeschränktes Sidecar-Modell) | zusätzlich 12–20 DW | nach stabiler kuratierter Version |
| Ungeprüfter Code ersetzt ganzen fachlichen Core (Control/Data-Plane-Trennung) | zusätzlich 20–35 DW | nicht empfohlen |

---

## 10. Was jetzt vs. was wartet (die eigentliche Antwort)

### 10.1 Jetzt (product-first, kuratiert, schmal)

Reihenfolge, an IST + Baustein-Workflow (role-dev → reviewer → fix → Suite → ff-merge) angepasst:

1. **C3-a: Vollständige Content-Signatur.** `PluginSigner` + Manifest auf das ganze Content-Set
   ausweiten (abhängige DLLs, UI-Bundles, CSS, Templates, Migrationen); Verifier lehnt nicht-gelistete
   Dateien ab. Direkte Fortsetzung von B1–B4; klein, self-contained; macht die Signing-Arbeit erst
   produktiv tragend („kein ausführbarer Inhalt außerhalb der signierten Liste").
2. **Ops: Calloras System-Plugins (Communication) im Release signieren** + Public-Key als
   Default-Trusted-Signer. Offener B4-Follow-up; zusammen mit #1 wird die Kette in Prod load-bearing.
   Danach ist der Per-Tier-Hard-Block (immer-signierte System-Plugins) baubar.
3. **C1: Extension-Point-Fläche.** Core-Services klassifizieren (Contributable/Decoratable/Replaceable/
   HostProtected); ~10 real dekorier-/ersetzbar machen; Analyzer „nur deklarierte Extension Points".
   Das ist die Produktfläche, die Plugins wertvoll macht — die DX-Hälfte, die sich gut überträgt.
4. **C6-a + C8-a (inkrementell): Resilienz + `HostProtected`-Katalog.** Timeouts/Cancellation/
   Circuit-Breaker für Runtime-Contributions; ad-hoc-`[HostProtected]` → getesteter Sicherheitskatalog
   mit Host-Auflösungstest. Schützt den shared-fate-Prozess unabhängig vom Tier.

### 10.2 Wartet (vollständig spezifiziert, Start ohne Neuerkundung)

- **C2 Boot-time-Override-Engine** (Restart-Semantik): erst nach C1 sequenzieren — das Inventar sagt,
  welche Services Boot-time statt Runtime brauchen. Per-Call-Proxy reicht für die nahe Fläche.
- **C4 Remote-Lifecycle, C5 Entitlement-Protokoll, C7 SDK**: brauchen einen realen Store-Gegenpart.
- **Gesamter Marketplace M0–M7 + Mangopay-Commerce**: Geschäftsentscheidung, gekoppelt an einen
  realen zahlenden Dritt-Publisher (offengehaltener Optionswert). Entscheidungsgate §7 zuerst.
- **Community-/Sandbox-Tier (§5.1)**: separates, schwächeres Modell. Dokumentierte Escape-Hatch, nicht
  spekulativ bauen.

---

## 11. Lieferreihenfolge (Phasen, wenn der Marketplace committed wird)

**Phase 0 (1–2 Wo):** C0 + ADR; Mangopay-Provider-Discovery; MoR-/Steuer-/Haftungsmodell festlegen;
10–20 repräsentative Core-Services als Extension-Point-Inventar; 2 Design-Partner + reale
Plugin-Anforderungen. *Exit:* Trust-Modell, Geldfluss, Override-Semantik schriftlich entschieden.

**Phase 1 (4–7 Wo):** vollständiges Paket-/Attestierungsmodell; Extension-Kategorien + geschützte
Flächen; erster boot-time Replace/Decorate-Pfad; Remote Download/Staging/atomarer Rollback; signiertes
Entitlement-Protokoll; UI-Paketabdeckung + CSP. *Exit:* geprüftes Testplugin ersetzt einen freigegebenen
Core-Service, bringt UI/API/Daten mit, wird aus dem Store installiert/entzogen/aktualisiert/
zurückgerollt.

**Phase 2 (6–9 Wo):** persistente Accounts/Publisher/Produkte/Releases; Mangopay-Onboarding +
Test-Geldfluss mit Provision; Reviewpipeline + Attestierung; Checkout/Order/Ledger/Entitlement/
Download; minimale Portale. *Exit:* externer Design-Partner veröffentlicht Release; Testkunde zahlt;
Publisheranteil erscheint beim Provider; Callora installiert nur das attestierte Paket.

**Phase 3 (4–8 Wo):** 5–10 reale Plugins + Update-/Rollback-Matrix; Refund/Chargeback/Abo-Ende/
Revocation/Key-Rotation testen; Last-/Ausfall-/Backup-/Incident-Übungen; Verträge/Datenschutz/
Rechnungen/SLAs; schrittweiser Rollout. *Exit:* Runbooks praktisch getestet; Geld-/Entitlement-/
Artefakt-Reconciliation ohne ungeklärte Differenzen.

---

## 12. Entscheidungs-Log

| Entscheidung | Alternativen | Begründung |
|---|---|---|
| Trusted in-process by Provenance | Out-of-Process/WASM-Sandbox für alle | Einzige ehrliche .NET-Antwort für tiefe Shopware-Freiheit; Sandbox kann Host nicht tief umschreiben. Zwei unabhängige Herleitungen decken sich. |
| Kuratiert als Dauerhaltung | offener Public-Marketplace | shared-fate + opake Binaries; offener Markt bräuchte separates schwächeres Modell |
| Signiertes Content-Manifest (ECDSA-P256) | Authenticode / NuGet-Signatur | Authenticode Linux-gebrochen; Content-Manifest cross-platform + deckt registry.json (B1–B4 erledigt) |
| Vollständige Content-Signatur als nächster Baustein | Marketplace-Layer zuerst | „kein Inhalt außerhalb signierter Liste" ist Voraussetzung für alles Externe; klein + tragend |
| **Mangopay** als Zahlungsanbieter | Paddle (verworfen), Stripe Connect (Fallback), Adyen (Scale) | Wallets + KYC/KYB + Payout eingebaut → geringeres MoR-/Steuer-Exposure für Callora; Preis: mehr Integration |
| Zwei-Tier-Trust: in-process dauerhaft / Sandbox als geringere Lane | „später alles für alle" | Community-Sandbox kann Host per Definition nicht tief umschreiben — bewusste Produktgrenze |
| Product-first: Marketplace an realen zahlenden Publisher gekoppelt | Marketplace jetzt bauen | Optionswert, nicht committet; verfrüht ohne Consumer |
| Boot-time-Engine nach Extension-Inventar | jetzt bauen | Inventar bestimmt, welche Services Boot-time brauchen; Per-Call-Proxy reicht nah |

---

## 13. Abnahmekriterien „sicher genug" (kuratierter Marketplace geht erst dann live)

- Kein ausführbarer/interpretierter Plugin-Inhalt außerhalb der signierten Content-Liste.
- Callora vertraut nicht allein dem Downloadkanal, sondern prüft Paket + Attestierung lokal.
- Unsignierte/unbekannte/zurückgerufene/inkompatible Pakete scheitern fail-closed.
- Ein Plugin kann einen freigegebenen Core-Service vollständig ersetzen.
- Ein Plugin kann keinen geschützten Service über den offiziellen Extension-Plan ersetzen.
- Neue Rechte / Core-Replacements benötigen erneuten Operator-Consent.
- Workspace-spezifische Exports laufen bei fehlender Availability nicht.
- Update/fehlgeschlagene Migration/gepinnter Runtime-Kontext → sichtbarer Fehler + definierter
  Rollback/Restart.
- UI-/API-/Daten-/Hintergrundanteile verschwinden bei Deaktivierung vollständig.
- Refund/Chargeback/Abo-Ende führen idempotent zur korrekten Entitlement-Entscheidung.
- Mangopay-Abrechnung, internes Ledger, Orders, Payout-Statements täglich reconciled.
- Revocation eines Publisher-Schlüssels + eines einzelnen Releases in einer Übung getestet.
- Marketing/Installationsdialog behaupten **keine** technische Sandbox für In-Process-Plugins.

---

## 14. Offene Fragen

- Mangopay-Partnerzugang/Konditionen für einen zentralen Plugin-Marktplatz DE/EU — Entscheidungsgate §7.
- Merchant-of-Record / Rechnungssteller final: Callora vs. Mangopay-Konstrukt (Steuer-/Rechtsberatung).
- Provisionshöhe + Reserve-/Auszahlungsrhythmus.
- Community-Tier: ob überhaupt, und wann die Sandbox-Lane wirtschaftlich lohnt.
- Versions-/Kompatibilitätsmatrix: wie viele parallele Callora-Versionen der Store unterstützt.
