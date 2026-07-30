# ADR-008: Workspace-Scoped Plugin Activation bei getrenntem Tenant-Begriff

Status: Accepted  
Date: 2026-04-18

## Context

Callora wird sowohl cloudfaehig als auch self-hosted betrieben.
Dabei sind `Tenant` und `Workspace` unterschiedliche Fachbegriffe:

- `Tenant`: Mandantenebene (insbesondere Cloud-Multitenancy)
- `Workspace`: operative Ebene fuer Teams/Arbeitsbereiche innerhalb eines Mandanten

Deployment-spezifische Auspraegung:

- Cloud: mehrere Tenants, je Tenant mehrere Workspaces
- Self-Hosted: genau ein Tenant (der Betreiber selbst), innerhalb davon mehrere Workspaces

Der Plugin-Lifecycle benoetigt weiterhin eine persistente workspace-spezifische Aktivierungszuordnung.

## Decision

1. Workspace-Scoped Runtime-Activation bleibt bestehen.
2. Die persistente Zuordnung `workspace_key x plugin_id` bleibt in `workspace_plugin_activations`.
3. API-Semantik fuer Entitlements wird auf `workspace` ausgerichtet:
   - Primarroute: `/api/plugins/workspaces/{workspaceKey}/entitlements/{pluginId}`
   - Legacy-Alias bleibt verfuegbar: `/api/plugins/tenants/{tenantId}/entitlements/{pluginId}`
4. Tenant-Begriff bleibt fuer Mandantenkontexte erhalten und wird nicht mit Workspace gleichgesetzt.
5. Self-Hosted wird fachlich nicht tenant-los modelliert, sondern als Single-Tenant-Betrieb mit Workspace-Segmentierung.

## Consequences

Positive:

- Klare Fachsprache im Workspace-Activation-Pfad.
- Kein Verlust von Cloud-Multitenancy-Semantik auf Tenant-Ebene.
- Rueckwaertskompatibilitaet fuer bestehende Tenant-Route-Clients.

Tradeoffs:

- Zwei Routen existieren parallel fuer denselben Read-Use-Case.
- Legacy-Route muss dokumentiert und spaeter geplant deprecatet werden.

## Guardrails

- Workspace-Scoped Activation darf keine globale Runtime-Activation implizieren.
- Tenant-Policies bleiben getrennt von Workspace-Entitlement-Zustaenden.
- Audit/Event-Metadaten fuer Workspace-Operationen enthalten immer `workspaceKey` und `scope=workspace`.
