# Callora Host Knowledge Index

Diese Datei ist der zentrale Einstieg fuer Wissen, Architekturentscheidungen und Compliance im Host-Repo.

## Zielbild / Strategie
- `Callora_Targetstruktur_fuer_KI.md`
- `docs/adr/ADR-007-host-centric-platform-split.md`
- `docs/portal/architecture/platform-model.md`
- `docs/portal/architecture/platform-bootstrap.md`

## Host API / Plugin-Lifecycle
- `docs/portal/architecture/host-backend-api.md`
- `docs/portal/modules/plugins.md`
- `docs/modules/PLUGIN_CONTRACT_V1.md`

## Compliance (DSGVO / EU AI Act)
- `docs/compliance/COMPLIANCE_BASELINE_DSGVO_EU_AI_ACT.md`

## Engineering-Richtlinien
- `ENGINEERING_RULES.md`
- `AGENTS.md`

## Repo-Orientierung
- `docs/REPO_MAP.md`

## Arbeitsprinzip fuer den Host
- Host bleibt die Control-Plane fuer Install/Activate/Deactivate/Uninstall.
- Plugins bleiben eigenstaendige Artefakte mit `registry.json`.
- Neue Features sollen event-/subscriber-faehig, DI-freundlich und DDD-konform aufgebaut werden.
