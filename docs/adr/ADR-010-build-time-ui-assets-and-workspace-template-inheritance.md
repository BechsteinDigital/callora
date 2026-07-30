# ADR-010: Build-Time UI Assets und Workspace-Template-Vererbung

Status: Accepted  
Date: 2026-04-18

## Context

PLAT-104 (Admin UI) und PLAT-105 (Workspace UI) benoetigen eine pluginfaehige UI-Erweiterung,
die Shopware-aehnlich vererbbar ist, aber die Laufzeitkomplexitaet gering haelt.

Gewuenschte Plugin-Struktur:

- `custom/plugins/<Plugin>/src/Resources/app/admin/src`
- `custom/plugins/<Plugin>/src/Resources/app/workspace/src`
- `custom/plugins/<Plugin>/src/Resources/views/workspace`

Zusaetzlich muss eine verwaltbare Template-Zuordnung pro Workspace existieren.

## Decision

1. UI-Assets von Plugins werden build-time in die jeweiligen Shells eingebaut, nicht dynamisch als fremder Code zur Laufzeit nachgeladen.
2. Plugin-Assets fuer `admin` und `workspace` folgen verbindlich den oben genannten Pfaden.
3. Workspace-Templates unter `Resources/views/workspace` werden versioniert registriert und per Policy/Zuordnung ausgewaehlt.
4. Template-Aufloesung fuer Workspace folgt einer deterministischen Vererbung:
   - `workspace override` -> `tenant default` -> `system default`
5. `api/*` bleibt die Control-Plane fuer Verwaltung (Template-Registry, Zuweisung, Aktivierung, Rollback).
6. `workspace/*` liefert nur die effektiv aufgeloesten Artefakte fuer den aktuellen Workspace.

## Consequences

Positive:

- Geringere Sicherheits- und Betriebskomplexitaet als Runtime-Code-Injection.
- Reproduzierbare Releases mit deterministischen Assets.
- Shopware-aehnliches Erweiterungsmodell mit klaren Override-Regeln.

Tradeoffs:

- UI-Aenderungen in Plugins erfordern Rebuild/Deploy.
- Erweiterungszyklen sind enger an Release-Pipelines gekoppelt.

## Guardrails

- Keine Umgehung von RBAC-, Scope- und Entitlement-Pruefungen durch Template-Zuordnung.
- Template-Auswahl ist pro Workspace auditierbar.
- Konflikte zwischen Plugin-Templates werden ueber feste Prioritaets- und Fallback-Regeln aufgeloest.
- `Admin UI` nutzt ausschliesslich `api/*`, `Workspace UI` ausschliesslich `workspace/*`.
