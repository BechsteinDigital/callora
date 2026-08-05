# Admin Onboarding Wizard — Design (P3, „Web-Install-Wizard")

**Datum:** 2026-07-27 · **Status:** akzeptiert · **Scope:** Admin-Shell (frontend-only)

## Understanding

Ein frisch installierter Callora-Deploy hat nach dem Console-Install (P2) + `.env`
genau: 1 Operator, 0 Workspaces, Communication-Plugin auto-aktiv. Der Onboarding-
Wizard führt den ersten Operator nach dem Login zu einem nutzbaren Setup. Er ist
**Onboarding nach Login**, kein Setup-Mode: die App ist bereits provisioniert, Secrets
und Operator kommen aus `.env`/Startup — der Wizard fasst KEINE Secrets/Domain an.

## Nicht-Ziele

- Keine Secret-/`.env`-/Domain-Behandlung in der UI (bleibt Console-Install/P2).
- Kein „unkonfiguriert booten"-Setup-Mode.
- Keine Backend-Migration, kein neuer Endpoint (nutzt bestehende Admin-APIs).
- Keine geräteübergreifende Persistenz des „gesehen/verworfen"-Status (Folgestufe).

## Schritte (MVP)

Willkommen → (1) ersten Workspace anlegen → (2) Plugins ansehen/aktivieren →
(3) ersten SIP-Account (Communication) → (4) weiteren Operator einladen → Fertig.

- **Workspace** wird INLINE im Wizard angelegt (Kernaktion, entsperrt `/`): minimaler
  Create (workspaceKey, displayName, type) → `PUT /api/workspaces/{key}`.
- **Plugins / SIP-Account / Nutzer** werden als geführte Schritte mit Status + Link auf
  die bestehende Vollansicht (`/plugins`, `/extensions/communication`, `/users/new`)
  gezeigt — keine Duplizierung vorhandener Views.

## Schritt-Status (server-autoritativ, abgeleitet)

- Workspace erledigt: `GET /api/workspaces` liefert ≥1.
- Plugins erledigt: `GET /api/plugins/installed` enthält ≥1 aktives (Communication ist
  auto-aktiv → i.d.R. schon erledigt).
- SIP erledigt: `GET /api/ext/admin/plugins/communication/sip-accounts?workspaceKey=<neuer WS>`
  nicht leer.
- Nutzer erledigt: `GET /api/users` liefert ≥2.

Fortschritt = Anzahl erledigter Schritte / 4. „Abgeschlossen" = alle 4 erledigt.

## Auslösung / Verbindlichkeit

- **Auto einmal:** 0 Workspaces UND localStorage-Merker `callora.onboarding.autoShown`
  nicht gesetzt → beim Login/Shell-Mount einmal Redirect auf `/onboarding`; Merker setzen.
- **Danach Karte:** Dashboard zeigt „Erste Schritte"-Karte (Fortschritt x/4, öffnet den
  Wizard) bis abgeschlossen ODER verworfen (`callora.onboarding.dismissed`).
- Jederzeit überspringbar; kein Zwang.

## Architektur (neues Modul `src/modules/onboarding/`)

- `onboarding.ts` — Composable: lädt Status aus den APIs, kapselt die localStorage-Merker,
  liefert `steps`, `completedCount`, `isComplete`, `shouldAutoRedirect()`, `markAutoShown()`,
  `dismiss()`, `isDismissed`.
- `OnboardingView.vue` — Wizard, Route `/onboarding` (unter der Auth-Shell). Schritt-Liste
  mit Status-Badges; Workspace-Inline-Form; Links für die übrigen; „Fertig" → Dashboard.
- `GettingStartedCard.vue` — Dashboard-Karte (Fortschritt, „Setup fortsetzen", „Verwerfen").
- Router: `/onboarding`-Child-Route. Auto-Redirect-Hook (im `routeGuard` oder AppShell-Mount).
- Dashboard: Karte einbinden, wenn `!isComplete && !isDismissed`.

## Decision Log

- **frontend-only + abgeleiteter Status + localStorage-Merker** (statt Backend-User-
  Preference + Aggregat-Endpoint): kleinster tragfähiger Schnitt, keine Migration, Server
  bleibt Wahrheitsquelle für „erledigt". Backend-Preference (geräteübergreifend) vertagt.
- **Workspace inline, Rest verlinkt:** die eine gateway-Aktion im Wizard, sonst keine
  Duplizierung vorhandener Views.

## Tests

Vitest für `onboarding.ts`: Status-Ableitung (fetch gemockt), Auto-Redirect-Logik
(0 Workspaces + Merker), Dismiss/Complete. `vue-tsc` sauber.
