# ADR-009: Code-First Extension Wiring (Shopware/Bundles statt Manifest-Wiring)

Status: Accepted  
Date: 2026-04-18

## Context

Callora orientiert sich architektonisch am Shopware-Prinzip:

- Plugins sind primaer code-seitige Erweiterungen.
- Erweiterungspunkte werden ueber Runtime-Mechanismen angebunden
  (z. B. Services, Subscriber, Provider), nicht primär ueber deklaratives Endpoint-Wiring.

Das bisherige Muster in Callora validierte `registry.json`-Extension-Eintraege
(`extensionPointId`, `surface`) bereits bei Install als hartes Gate.
Das ist strenger als das angestrebte Bundle-/code-first Modell.

## Decision

1. Extension-Wiring wird in Callora code-first umgesetzt.
2. `registry.json` bleibt Governance-Metadatenquelle (Contract, Version, Security, Basis-Metadaten),
   ist aber nicht mehr verpflichtende Quelle fuer Extension-Wiring.
3. Harte Validierung von Extension-Point/Surface/Scope wird vom Install-Zeitpunkt
   auf Runtime-Registrierung verschoben.
4. Runtime-Registrierungen aktiver Plugins werden gegen den Host-Extension-Point-Katalog validiert.
5. Dedizierte oeffentliche `api/extensions/*` oder `workspace/extensions/*` Endpoints
   bleiben nicht Teil dieses Modells.

## Consequences

Positive:

- Naeher am Shopware-/Bundle-Entwicklermodell.
- Weniger deklarativer Pflegeaufwand in `registry.json`.
- Governance bleibt erhalten (Runtime-Gates, Scope-/Surface-Pruefung).

Tradeoffs:

- Fehler in Extension-Wiring werden spaeter sichtbar (bei Aktivierung statt Installation).
- Gute Runtime-Diagnostik (Audit/Reason-Codes) wird noch wichtiger.

## Guardrails

- Keine Umgehung von Scope-/Surface-Pruefungen fuer Runtime-Registrierungen.
- Kein Host-Internal-Zugriff als Plugin-Vertrag; nur ueber definierte Contracts.
- Tenant-/Workspace-Policies bleiben auch fuer Runtime-Extensions bindend.
