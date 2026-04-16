# Platform Model (Engine + Host + Plugins)

Callora verfolgt ein host-zentriertes Plattformmodell:

- `Engine`: Telekommunikationskern (SIP, RTP, RTCP, SRTP, Media)
- `Host`: Control Plane + Product Plane (Tenants, Users, Trunks, APIs, Entitlements)
- `Plugins`: erweiterbare Feature-Domaenen (Dialer, Contact Center, AI, Risk, Policy)

## UI Surfaces

Callora nutzt folgende Begriffe:

- `Admin UI`: Betreiber-/Backoffice-Oberflaeche
- `Workspace UI`: Nutzer-/Agenten-Oberflaeche

Der Begriff `Storefront` wird im Callora-Kontext nicht genutzt.

## Runtime Integration

Der Host verwaltet den Plugin-Lifecycle:

1. `install`
2. `activate`
3. `deactivate`
4. `uninstall`

Aktive Plugins koennen Backend-Services und UI-Erweiterungen fuer Admin/Workspace registrieren.

Die Lifecycle-API ist dabei nicht oeffentlich offen:

- Mindestschutz in Phase 1: API-Key Auth
- Zielschutz: tenantfaehige Rollen-/Rechte-Policies

## Compliance

Plattformweite Anforderungen:

- DSGVO: Datenminimierung, Zweckbindung, Export/Loeschpfade, Audit.
- EU AI Act: AI-Feature-Register, Human Oversight, Traceability.

Details:

- `docs/compliance/COMPLIANCE_BASELINE_DSGVO_EU_AI_ACT.md`
- `docs/modules/PLUGIN_CONTRACT_V1.md`
