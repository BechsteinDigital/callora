# Admin-Shell — Baustein 1: Walking Skeleton (Design)

Datum: 2026-07-17 · Status: Design (zur Freigabe)

## Kontext

Die Admin-Shell wird neu aufgebaut. Der frühere Ansatz — freistehende `apps/admin-shell`
(Nuxt, Glassmorphism, analog `apps/workspace-shell`) — ist verworfen. Neuer Weg: die
Admin-Shell **colocated im Administration-Modul** (Shopware-Prinzip), als Static-Web-Assets
ins Distributionspaket gebündelt.

Das Auth-/Scope-Fundament steht bereits (Phase B): der gemeinsame Admin-Login
`POST /api/auth/login` (Cookie-Session, alle Admin-Ebenen), der effektive Kontext-Endpunkt
`GET /api/admin/context`, und auditierte serverseitige Scope-Guards.

## Zerlegung der Admin-Shell (Kontext für dieses Baustein)

Die Admin-Shell ist zu groß für ein Spec. Sie zerfällt in drei Bausteine mit eigenen
Design→Plan→Bau-Zyklen:

1. **Walking Skeleton** (dieses Dokument): SPA aufsetzen, Build-/Distributions-Pipeline,
   Design-Token- + Headless-Fundament, App-Shell-Layout, Auth-Integration, ein Durchstich-Screen.
2. **Native Subsystem-Screens** (später): Users/RBAC, Workspaces, Plugins, SystemConfig
   (generische Schema-UI), Flows, Media, Webhooks, Jobs … gegen die vorhandenen APIs.
3. **Plugin-Admin-Extension-Architektur** (zuletzt, use-case-getrieben ab erstem Plugin mit
   Admin-UI): wie Plugins Navigation/Seiten/Widgets in die SPA injizieren; konsumiert die
   vorhandenen `/api/ext/admin/*`-Endpunkte. Ersetzt das alte `shell-core`.

## Tragende Architektur-Entscheidungen

- **Stack:** frischer Vue-3-SPA (Vite + TypeScript), kein Nuxt/SSR. Die bestehende
  Nuxt-basierte `shell-*`-Basis bleibt bei den Alt-Shells und wird **nicht** übernommen
  (bewusster Tech-Neuanfang).
- **UI-Schicht:** Headless-Primitives (**Radix Vue / Reka UI**) + **eigenes SCSS-Design-Token-System**.
  Kein visuelles Framework (Vuetify/PrimeVue/Quasar) — dessen opinioniertes Theming kollidiert
  mit der White-Label-Token-Achse und würde Host↔Plugin an eine Framework-Version koppeln.
  **Nicht** shadcn-vue (bringt Tailwind mit, widerspricht „eigene Tokens").
- **Distribution:** Weg A — das Administration-Modul bündelt die SPA als Static-Web-Assets,
  Callora-Production erbt sie über die Referenz/das Paket (siehe unten).

## Verzeichnisstruktur (colocated, Shopware-Muster, schlank)

```
src/Administration/
  Resources/app/administration/        # Frontend-Quelle (Vue 3 + Vite + TS)
    src/
      main.ts, App.vue
      app/           # Shell-Framework: AppShell.vue, router.ts
      core/          # Services/State: http.ts, auth-Store, design/tokens.scss, ui/ (Radix-Wrapper)
      modules/       # ein Ordner je Feature (hier zunächst nur dashboard/, auth/)
    index.html · package.json · vite.config.ts · tsconfig.json
  wwwroot/admin/                       # Vite-Build-Output (generiert, Static-Web-Asset)
  Callora.Administration.csproj        # StaticWebAssetBasePath=admin; MSBuild-Target baut das Frontend
```

Gliederung `app`/`core`/`modules` übernimmt Shopwares Prinzip (Shell / Services / Feature-Module),
ohne dessen Ballast (blocks-list.json, Twig, code-mods, Feature-Flags).

## Build & Distribution

- `vite build` → `src/Administration/wwwroot/admin/` (gehashte Assets + `index.html`).
- **MSBuild-Target** in `Callora.Administration.csproj` hängt `npm ci && npm run build` vor den
  C#-Build → ein `dotnet build` baut alles nahtlos mit. Opt-out via `-p:SkipAdminFrontend=true`
  für reine Backend-Iteration (kein Node nötig).
- Das Web-SDK trägt `wwwroot/**` als Static-Web-Assets; `<StaticWebAssetBasePath>admin</StaticWebAssetBasePath>`
  mountet sie unter `/admin/` (statt `_content/Callora.Administration/`).
- **Distribution:** Callora-Production referenziert Administration (heute ProjectReference,
  später NuGet) und **erbt die Static-Web-Assets automatisch**. `dotnet publish Callora.Production`
  sammelt sie in den Publish-Output — das Host-wwwroot entsteht generiert, niemand legt es manuell ab.
  Callora-Production bleibt schlank.

## Serving & Routing (Host)

- `UseStaticFiles()` (bereits vorhanden) liefert die Assets.
- **Neu:** SPA-Fallback unter `/admin/*` → `admin/index.html` für Client-seitiges Routing.
  `/admin` kollidiert mit keinem Reserved-Prefix (alle sind `/api/*` oder `/workspace/*`).
- **Implementierungs-Caveat (beim Bau zu verifizieren):** `MapFallbackToFile` sucht standardmäßig
  im physischen Host-wwwroot, nicht in den Static-Web-Assets. Mit `StaticWebAssetBasePath` kann ein
  kleiner Custom-Fallback nötig sein, der das Static-Web-Asset `admin/index.html` ausliefert. Kein
  Architektur-Risiko, aber ein zu klärendes Detail.

## Auth-Integration (nutzt Phase-B-Fundament, keine neuen Auth-Endpunkte)

- `/admin/login`: Formular (Login, Passwort, optional Workspace-Key) → `POST /api/auth/login`
  → Cookie-Session.
- Danach `GET /api/admin/context` → Identity/Rollen/Permissions/Scope/IsOperator in den Auth-Store.
  Navigation + Sichtbarkeit werden daraus abgeleitet; **serverseitige Autorisierung bleibt maßgeblich**
  (UI-Ausblendung ist keine Sicherheitsgrenze, ADR-014 §3.4).
- Route-Guard: kein Kontext → Redirect `/admin/login`. HTTP-Client fängt `401` ab → Redirect.
- Logout → `POST /api/auth/logout` (Cookie clear).

## UI-Fundament

- **Design-Tokens** (`core/design/tokens.scss`): CSS-Custom-Properties für Farbe/Spacing/Typo/
  Radius/Shadow — die White-Label-Achse (später pro Tenant überschreibbar).
- **UI-Primitives** (`core/ui/`): schlanker Satz gestylter Wrapper (Button, Input, Dialog,
  DropdownMenu, …) auf Radix Vue + Tokens.
- **App-Shell** (`app/AppShell.vue`): Sidebar-Navigation + Topbar (User-Menü/Logout);
  vue-router im History-Mode mit base `/admin`.

## Durchstich-Screen

**Dashboard** (minimal). Zeigt den geladenen Admin-Kontext (wer, Rolle/Scope, Permissions).
Beweist die volle Kette Login → Cookie → `/api/admin/context` → kontextabhängig gerenderte UI,
**ohne** weitere Subsystem-APIs (die sind Baustein 2). Ehrlicher, schmaler Durchstich.

## Testing

- **Frontend (Vitest):** `core`-Logik (http-Client, auth-Store, Context-Parsing) + Component-Tests
  für Login-Formular und Route-Guard. Kein E2E im Skeleton (Playwright erst mit mehr Oberfläche).
- **Backend (ein Integrationstest):** der Host liefert `/admin` (SPA-Fallback → `index.html`) aus und
  die Static-Web-Assets sind gemountet. Sichert die Serving-/Distributions-Verdrahtung ab (der
  riskanteste Teil dieses Bausteins).

## Nicht-Ziele (bewusst außerhalb Baustein 1)

- Native Subsystem-Screens (Baustein 2).
- Plugin-Admin-Extension-Architektur (Baustein 3).
- Pro-Tenant-White-Label-Runtime-Theming (Tokens werden vorbereitet, aber nicht pro Tenant geladen).
- E2E-Tests, i18n, Design-Politur über das Nötige hinaus.

## Erfolgskriterien

1. `dotnet build` baut das Frontend mit (bzw. überspringt es bei `SkipAdminFrontend=true`).
2. Der laufende Host liefert unter `/admin` die SPA aus; unangemeldet landet man auf `/admin/login`.
3. Login über `/api/auth/login` funktioniert; danach zeigt das Dashboard den echten Kontext aus
   `/api/admin/context`.
4. Callora-Production erbt die Assets ohne eigenes Ablegen (Publish-Smoke).
5. Vitest- und der Backend-Serving-Integrationstest sind grün.
```
