# ADR-013: Trust-Modell — Trusted-in-process by Provenance

Status: Accepted
Date: 2026-07-14

## Context

Ein Code-Review (Codex 5.6, #1) hat zu Recht festgestellt: Die in
`CALLORA_ZIELARCHITEKTUR_DOMAENENNEUTRALE_PLUGIN_PLATTFORM_REV2.md` §2.2/§2.3 formulierten
Core-Invarianten lassen sich gegenüber **nicht vertrauenswürdigen** Plugins technisch **nicht**
garantieren.

Technische Realität von .NET:

- Code Access Security (CAS) und AppDomain-Sandboxing wurden mit .NET Core entfernt und kommen nicht
  zurück. Es gibt **keinen unterstützten In-Process-Sandbox**.
- `AssemblyLoadContext` isoliert **Typen und Versionen**, nicht **Fähigkeiten**: ein in-process
  geladenes Plugin kann Reflection, Dateisystem, P/Invoke, eigene Threads und Netzwerk nutzen.
- Die kuratierte DI (`CuratedPluginServiceProvider`) beschränkt nur, was **injiziert** wird, nicht,
  was der Code **tut**. `internal`/`[CalloraInternal]` sind per Reflection umgehbar.

Kernsatz: **„Untrusted in-process" existiert in .NET nicht.** Die einzige echte Sandbox-Grenze ist die
**Prozessgrenze** (out-of-process + IPC). Damit reduziert sich die Designfrage auf zwei Optionen:

- **A — Trusted in-process (Governance):** Vertrauen über Signatur + Provenance + Vetting +
  Operator-Consent. Volle Erweiterbarkeit (Decoration/Events/Contributor). Präzedenz: Shopware,
  Orchard, Umbraco, ABP.
- **B — Untrusted out-of-process (Sidecar/IPC):** echte Prozess-Sandbox, aber nur eine schmale
  RPC-Fläche (keine In-Process-Decoration/Events), zweites Plugin-Programmiermodell, hohe Kosten.

Alle heutigen und geplanten Plugins (Communication, Dialer, Video, AI Agent, Contact Center — REV2
§2.5) sind **First-Party/kommerziell**, also zurechenbare, signierbare Pakete — kein anonymer
Fremd-Code.

## Decision

1. **Callora folgt Modell A: Trusted-in-process by Provenance.** Alle Plugins laufen in-process und
   sind damit vertrauenswürdig. „Untrusted in-process" ist **kein unterstütztes Konzept**.
2. **Vertrauen wird über Herkunft etabliert:** Paket-Signatur + Publisher-Trust + (Marketplace)
   Vetting + **expliziter Operator-Consent bei der Installation**. Das ist das bewährte
   Shopware-Modell.
3. **Trust-Tiers** (alle in-process/trusted):
   - **System/Foundation** — gebündelt (Communication), voll vertrauenswürdig.
   - **Verified/Commercial** — von BechsteinDigital signiert.
   - **Community-signed** (später) — von bekanntem Publisher signiert; Installation verlangt
     expliziten Consent („dieses Plugin läuft mit vollem Zugriff — installiere nur bei Vertrauen zum
     Autor").
4. **Reframe von REV2 §2.3 (ehrlich):** `internal`/`[CalloraInternal]` + kuratierte DI sind die
   **definierte, sichere Erweiterungsfläche** — Footgun-Schutz für wohlerzogene Plugins und der
   Vertrag. Sie sind **kein Sicherheits-Boundary gegen bösartigen In-Process-Code**. Die Garantie
   gegen bösartige Plugins ist **Governance** (Signatur/Vetting/Consent), nicht die Runtime.
5. **Out-of-process Sidecar/IPC (Modell B) bleibt dokumentierte Zukunfts-Ausfahrt** — nur relevant,
   falls je ein *offener, ungeprüfter* Publikums-Marktplatz Ziel wird. Wird **nicht jetzt** gebaut.
6. **Offener, ungeprüfter Fremd-Marktplatz ist bewusst NICHT das Ziel.** Die Distribution ist
   kuratiert/verifiziert (BechsteinDigital + geprüfte Partner). Damit reicht Modell A dauerhaft.
7. **Das Paket-/Signaturmodell (H7, REV2 §12 → Phase 3) ist der Enabler** dieses Trust-Modells:
   Signatur = Provenance = Grundlage der Trust-Entscheidung. Content-Checksum, Revocation und
   Provenance-Chain gehören damit zum Rückgrat, nicht nur zur „Paket-Härtung".

## Consequences

Positiv:

- Einfach, bewährt (Shopware seit Jahren), keine IPC-Kosten, volle In-Process-Erweiterbarkeit bleibt.
- Konsistent mit dem bestehenden ALC-Modell und REV2 §2.2 (Extension-Mechanismen).

Tradeoffs:

- Ein bösartiges in-process Plugin *kann* Core-Invarianten aushebeln — abgefedert durch
  Signatur/Vetting/Consent, nicht durch die Runtime. Das wird offen kommuniziert statt technisch
  vorgetäuscht.
- Für *wirklich* ungeprüften Fremd-Code ungeeignet ohne die (nicht gebaute) Sidecar-Ausfahrt.

## Guardrails

- Installation eines Community-signed Plugins verlangt expliziten Operator-Consent mit klarer
  „voller Zugriff"-Warnung.
- Doku/Marketing versprechen **keinen** technischen Sandbox gegen bösartige Plugins; die Zusage ist
  Provenance + Kuratierung.
- Der Plugin-Vertrag wird dort, wo es billig ist, RPC-tauglich gehalten (schmale Teilmenge), um die
  Sidecar-Ausfahrt offenzuhalten — ohne jetzt IPC zu bauen.

## Refines / Relates

- Verfeinert REV2 §2.3 (Core-Invarianten als Governance-, nicht Runtime-Garantie; dort mit Verweis
  annotiert).
- Rahmt H7 / das Paket-Signaturmodell (REV2 §12 → Phase 3) als Trust-Enabler.
- Ergänzt ADR-012 (Extensibility): die breite In-Process-Fläche setzt vertrauenswürdige Plugins voraus.
