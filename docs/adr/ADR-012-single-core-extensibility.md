# ADR-012: Ein-Core-Extensibility (Breite Sichtbarkeit, kontrollierte Erweiterbarkeit)

Status: Accepted
Date: 2026-07-14

> **Verfeinert durch REV2 (`CALLORA_ZIELARCHITEKTUR_DOMAENENNEUTRALE_PLUGIN_PLATTFORM_REV2.md` §2.2/§7):**
> „Freiheit über Schutz" wurde präzisiert — Sichtbarkeit ist **kein** automatischer Extension Point.
> Es gelten drei getrennte Stufen (sichtbar / erweiterbar / ersetzbar); Decoration nur auf
> ausdrücklich mit `[CalloraExtensible]` markierten Services.

## Context

Callora steuert auf eine domänen-neutrale Plugin-Plattform zu (siehe
`docs/CALLORA_PLUGIN_PLATTFORM_ZIELARCHITEKTUR.md`). Zwei frühere Festlegungen etablierten ein
**schutz-orientiertes** Erweiterungsmodell:

- `SHOPWARE_PRINZIPIEN_FUER_CALLORA.md` Prinzip 4 „Contracts statt Internals" — Plugin-Integration
  nur über versionierte, dokumentierte Schnittstellen.
- ADR-009-Guardrail „Kein Host-Internal-Zugriff als Plugin-Vertrag; nur über definierte Contracts."

Das Zielbild verlangt jedoch **maximale Erweiterungsfreiheit** (Shopware-Modell: ein Plugin darf
jeden Service dekorieren, jedes Event abonnieren — bis hin zu `UserDataExport`). Technischer Zwang:
Decorate-Anything erfordert volle Typ-Sichtbarkeit — ein dünnes `Core.Abstractions`-Paket (ABP-Modell)
würde die Fläche verstecken und damit gegen das Ziel arbeiten. Ein Plugin kann nur dekorieren, was es
referenzieren kann.

## Decision

1. **Ein `Callora.Core`** — kein separates `Core.Abstractions`. `Callora.Administration` und
   `Callora.Workspace` werden nur zur Deployment-Modularität herausgetrennt, bleiben aber voll
   erweiterbar.
2. Plugins **sehen** die öffentliche Core-/Administration-/Workspace-Oberfläche, aber Sichtbarkeit
   allein erlaubt kein Ersetzen. Drei getrennte Stufen (REV2 §2.2): **sichtbar** (referenzierbar) /
   **erweiterbar** (ein `[CalloraExtensible]`-markierter Extension Point, implementier-/beitrag-/
   dekorierbar) / **ersetzbar** (nur ausdrücklich freigegebene Services). Erweiterung ausschließlich
   über Events, Contributor/Registration, Plugin-Controller, markierte Decoration und öffentliche
   Plugin-APIs anderer Plugins.
3. **Prinzip 4 „Contracts statt Internals" wird abgelöst** durch (REV2 §11): „Breite Sichtbarkeit,
   Erweiterung ausschließlich über dokumentierte Mechanismen und explizite Marker; Schutz durch echte
   interne Grenzen bei Sicherheit, Compliance und Governance."
4. **ADR-009 wird verfeinert:** Der Mechanismus über eine breite sichtbare Fläche *ist* das
   Vertragsmodell — nicht ein schmaler kuratierter Contract.
5. Sicherheitskritische Pfade (Auth-/RBAC-Enforcement, Tenant-Isolation, Secrets/DataProtection,
   Plugin-Lifecycle-Governance) sind `internal` bzw. `[CalloraInternal]` und **nicht** dekorierbar.

## Consequences

Positiv:

- Maximale Entwicklerfreiheit, nah am Shopware-/Bundle-Modell.
- Kein Abstractions-Ceremony; Core-intern gilt Feature-first-Kolokation (Interface neben Implementierung).
- Konsistent mit der früheren „Abstractions-Layer auflösen"-Entscheidung.

Tradeoffs:

- Öffentliche Core-Signaturen werden **De-facto-Verträge** → Stabilität per Konvention/Marker +
  Deprecation-Disziplin statt per Paketgrenze (analog Shopwares `@internal`/`@deprecated`).
- **Type-Identität** erzwingt, dass Core (+ Administration + Workspace) im **geteilten Load-Context**
  unifiziert geladen wird (Plugin-ALCs isolieren Core-Typen nicht) — via
  `SharedContractAssemblyRegistry` (PLAT-256).

## Guardrails

- Nie Core-Dateien direkt editieren; Erweiterung ausschließlich über die Mechanismen.
- Kein Vorbeigreifen an den Mechanismen (keine Reflection auf `internal`/private Member).
- `internal`-markierte Sicherheitspfade werden nicht dekoriert.
- Breaking Changes an öffentlichen Signaturen nur mit Deprecation-Layer + Major-Version-Kadenz.

## Supersedes / Refines

- Löst `SHOPWARE_PRINZIPIEN_FUER_CALLORA.md` Prinzip 4 ab (dort mit Verweis annotiert).
- Verfeinert ADR-009-Guardrail „Kein Host-Internal-Zugriff".
