# Surface-Template-Engine — Implementierungs-Spec (ADR-015 Phase I)

**Status:** Draft (autonom entschieden, User-Auftrag „alles umsetzen, damit Plugins darauf aufbauen")
**Datum:** 2026-07-19
**Bezug:** ADR-015 (Engine-Wahl Scriban), ADR-014 §8–§11/§15 (SurfaceShell, Bundle-Achsen, Reihenfolge), ADR-013 (Trust)

---

## 1. Ausgangslage (Ist-Stand verifiziert)

- **Kein Server-SSR heute.** Die tenant-facing Fläche ist ein Client-SPA-Bootstrap:
  `Callora.Workspace` → `/workspace/public/resolve` (Host/Pfad→Workspace) und
  `/workspace/public/bootstrap.js` (injiziert `window.__CALLORA_WORKSPACE_CONTEXT__`).
- **Kompositions-Fundament da, rendert nichts.** `IWorkspaceTemplateResolutionService`
  (→ `WorkspaceTemplateEffectiveSnapshot[]`: TemplateKey/PluginId/Version/TemplatePath/
  ParentTemplateKey/Priority/Source) + `WorkspaceUiChainResolver` (geordnete Plugin-UI-Kette)
  + `IWorkspaceTemplateResolutionCache` (InvalidateWorkspace/InvalidateAll).
- **Keine Engine.** CPM enthält kein Template-Paket.
- Token-Achse (Theme) ist live (`WorkspacePublicThemeResolver` + Admin-Views fertig).

## 2. Entscheidungen (offene ADR-015-Punkte, autonom geschlossen)

- **DECISION Paket:** Neues `src/Surface.Rendering/` (`Callora.Surface.Rendering`, `Microsoft.NET.Sdk.Web`-Library),
  referenziert Core (+ Workspace). Trägt Scriban isoliert. Umbenennung `Callora.Workspace → Callora.Surface`
  (ADR-015 §4) VERTAGT — rein mechanisch, eigener Baustein E7 (PublicAPI-Churn), kein Blocker.
- **DECISION Engine:** `Scriban` als PackageReference (CPM), nur in Surface.Rendering.
- **DECISION Sandbox (untrusted-by-default, ADR-015 §8):** `TemplateContext` mit explizit gebautem
  `ScriptObject` (Allowlist der Surface-Context-Werte), `MemberFilter` verweigert Reflection/.NET-Typ-Durchgriff,
  `LoopLimit`/`RecursiveLimit`, Render-Timeout via `CancellationToken` + Wanduhr-Guard, Output-Grössenlimit,
  `ITemplateLoader` mit Tiefenlimit. Kein Relaxed Member Access.
- **DECISION Kompositions-Syntax (`block`/`extends`/`parent()`):** Twig-nahe Custom-Statements auf Scriban,
  über den bundle-aware `ITemplateLoader` + einen Pre-Compile-Pass aufgelöst:
  - `{{ extends "@Bundle/pfad.html" }}` — Datei-Vererbung (Kette Surface→Workspace→Distribution).
  - `{{ block name }} … {{ end }}` — überschreibbarer Block; `{{ parent }}` rendert die Basisversion.
  - Datei-Override ohne `extends` = vollständiger Ersatz desselben logischen Pfads über die Prioritätskette.
  - Namespace `@BundleId/relpath`. Zyklen → Kompilierfehler (kein Laufzeit-Hang, ADR-014 §9.6).
- **DECISION Cache:** In-Memory (bestehende `IWorkspaceTemplateResolutionCache`-Semantik erweitern bzw. eigener
  `ISurfaceRenderCache`), Key = Tenant×Workspace×Surface×Template-Chain×Plugin-Versionen×Locale (ADR-015 §7);
  Invalidierung an Plugin-Lifecycle (bereits ereignisgetrieben vorhanden).
- **DECISION Trust:** Template-Bundles laufen im ADR-013-Modell; Rendering fügt keine Vertrauensannahme hinzu,
  härtet aber die Sandbox aktiv.

## 3. Baustein-Zerlegung (Walking-Skeleton-first, ADR-014 §15.3)

- **E1 — Walking Skeleton (dieser Baustein zuerst):** `Callora.Surface.Rendering`-Paket + Scriban + gehärtete
  Sandbox + `SurfaceShell`-Basistemplate (ADR-014 §8.1) + `ISurfaceRenderer` (rendert EIN Template mit dem
  Surface-Context, single-level) + öffentlicher Render-Endpoint (Host/Pfad→Surface→HTML). Für SPA-Root-Templates
  genügt das (`<div id="callora-app" data-workspace data-surface>`, §11.2) — die vorhandenen Shells laufen als
  je eine Surface. Contract-Tests: Sandbox verweigert Reflection/Loop-DoS; SPA-Root rendert erwartetes HTML;
  unbekannte Surface → 404.
- **E2 — Bundle-aware `ITemplateLoader`:** lädt Template-Dateien der Bundles entlang der Prioritätskette
  (Namespace-Auflösung, Datei-Override).
- **E3 — Kompositions-Compiler (Baustein B, der harte Teil):** `extends`/`block`/`parent()`, mehrstufig,
  Datei- + Block-Override, deterministische Reihenfolge (ADR-014 §9.6), Zyklen-Erkennung.
- **E4 — Render-Cache + deterministische Invalidierung** (ADR-015 §7).
- **E5 — SurfaceShell-Basis-Views + Referenz-Template-Bundle** (ein Document-Bundle als lebendes Beispiel).
- **E6 — Contract-Tests** (Datei-/Block-Override, `parent()`, Prioritätsreihenfolge, Zyklen-Fehler).
- **E7 — Umbenennung `Callora.Workspace → Callora.Surface`** (mechanisch, zuletzt).

## 4. Nicht-Ziele (Phase I)

Page-Builder (ADR-014 §10.4), Asset-Pipeline SCSS→CSS/JS-Bundling (eigener Entscheidungspunkt, ADR-015 §11),
verteilter Cache, Prerendering für SPA-Islands. Fachlogik bleibt in Feature-Plugins, nicht im Template.

## 5. Sicherheits-Fokus (E1 kritisch)

Die Sandbox ist der sicherheitsrelevante Kern. E1 liefert bereits: Allowlist-`ScriptObject`, `MemberFilter`
(kein Typ-/Reflection-Durchgriff), Loop-/Recursive-Limit, Output-Cap, Loader-Tiefenlimit. Jeder dieser Guards
bekommt einen Negativ-Test (Angriff schlägt fehl), nicht nur den Happy-Path.
