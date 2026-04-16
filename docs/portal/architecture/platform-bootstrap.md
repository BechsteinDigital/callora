# Platform Bootstrap (Startpunkt)

Das Zielbild ist: `Engine + Host + Plugins` mit runtime-faehiger Erweiterung.
Der Startpunkt dafuer ist ein klarer Minimal-Stack, der sofort umgesetzt werden kann.

## Begriffe

- `Admin UI`: Betreiber-/Backoffice-Oberflaeche
- `Workspace UI`: Agenten-/Nutzer-Oberflaeche

`Storefront` wird im Callora-Kontext nicht verwendet.

## Minimaler Tech-Stack (Phase 1)

- `Engine (dieses Repository)`: SIP/RTP/RTCP/SRTP + Media + `VoipClient`
- `Host Backend`: ASP.NET Core Web API + PostgreSQL + Redis + OpenTelemetry
- `Host Backend (jetzt)`: ASP.NET Core Web API + SQLite (Lifecycle/Audit) + API-Key Auth + Swagger
- `Host Backend (ziel)`: PostgreSQL + Redis + OpenTelemetry + zentrale Entitlement-/Policy-Services
- `Plugin Runtime`: `Callora.Hosting` + `ICalloraPluginRuntime` (install/activate/deactivate/uninstall)
- `Admin UI`: React + TypeScript (Betrieb, Tenants, Entitlements, Plugin-Lifecycle)
- `Workspace UI`: React + TypeScript (Agentenfunktion, Dialer-/CC-Views)

## Plugin-Faehigkeit ab jetzt

Runtime-Plugins koennen ohne Neustart geladen werden.
Zusatz: der Plugin-Katalog unterstuetzt mehrere Exporte pro Vertragstyp
(z. B. mehrere UI-Extension-Provider parallel).

## Was als Naechstes umgesetzt wird

1. Host-API fuer Plugin-Lifecycle (`/plugins/install|activate|deactivate|uninstall`).
2. Signierte Plugin-Pakete (Manifest + Signatur + Kompatibilitaetspruefung).
3. UI-Extension-Registry im Host (Admin UI + Workspace UI getrennt).
4. Tenant-/Lizenz-Entitlements als Host-Policy vor Plugin-Aktivierung.
5. Audit-/Compliance-Trail fuer alle Lifecycle-Operationen.

## Compliance by Default

- DSGVO: Datenminimierung, Loesch-/Exportpfade, Zweckbindung, Audit.
- EU AI Act: AI-Register, Human Oversight, Traceability fuer AI-Features.

Siehe auch:

- `docs/compliance/COMPLIANCE_BASELINE_DSGVO_EU_AI_ACT.md`
- `docs/modules/PLUGIN_CONTRACT_V1.md`
