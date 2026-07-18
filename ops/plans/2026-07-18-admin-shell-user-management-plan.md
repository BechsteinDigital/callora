# Admin-Shell: Benutzer-/Operator-Management (Plan)

Datum: 2026-07-18 · Task #30 (Admin-Shell), erstes echtes Feature-Modul nach dem
Walking Skeleton. Repo: callora, `src/Administration/Resources/app/administration`.

## Ziel

Ein Operator verwaltet Benutzer über die Admin-Shell: auflisten, anlegen,
bearbeiten, löschen — inklusive Rollenzuweisung (was einen Benutzer zum Operator
macht). Etabliert das CRUD-Muster für alle folgenden Admin-Module.

Bootstrap des allerersten Operators (wenn noch keiner existiert) ist ein
SEPARATER Baustein (Core/Backend), nicht Teil dieses UI-Bausteins.

## Backend (existiert, wird nur konsumiert)

- `/api/users` (gated `user.*`, operator-/workspace-scoped): GET / (Liste),
  GET /{id}, POST / (nur Operator), PUT /{id}, DELETE /{id} (GDPR-Erasure).
  Response `BackendUserApiResponse(ExternalId, Email?, DisplayName?, HasPassword,
  …, CreatedAtUtc, UpdatedAtUtc)`; Create `(ExternalId, Email?, DisplayName?,
  Password)`; Update `(Email?, DisplayName?, Password?)`.
- `/api/security/rbac` (gated `role.*`): GET /roles → `(Role, Permissions[])`;
  GET /users → `(UserId, Role)` (globale Zuweisungen); PUT /users/{id} `(Role)`.

## Kernentscheidungen (DECISION-Log)

1. **Zwei Permission-Achsen, getrennt gated (Security-kritisch):** User-CRUD an
   `user.read/create/update/delete`, Rollenzuweisung an `role.read/role.update`.
   Ein Workspace-Admin (nur `user.*`) sieht/bearbeitet Benutzer, kann aber KEINE
   Rollen zuweisen (sonst Self-Escalation zu SuperAdmin). Die UI blendet die
   Rollen-Spalte/-Aktion aus, wenn `role.*` im Kontext fehlt.

2. **Rolle als integraler Teil der Benutzeransicht:** Liste verknüpft
   `/api/users` (Identität) mit `/api/security/rbac/users` (Rolle je UserId).
   Rollenoptionen aus `/api/security/rbac/roles`. So ist „Operator = User +
   superadmin-Rolle" direkt sichtbar/setzbar.

3. **Eigene Detail-Routen** (`/users`, `/users/new`, `/users/:userId`) statt
   Modal — etabliert das skalierbare Detail-View-Muster für Folge-Module.

4. **Dünner Service + Vitest**, konsistent zum `auth`-Modul: `usersApi.ts` als
   `apiFetch`-Wrapper, Fehlerbehandlung über die Problem-Details-Antworten.

## Dateien (neu, unter `src/`)
- `modules/users/usersApi.ts` — Service (User-CRUD + Rollen).
- `modules/users/usersApi.test.ts` — Service-Tests (Fetch gemockt).
- `modules/users/UsersListView.vue` — Tabelle + Aktionen (permission-gated).
- `modules/users/UserDetailView.vue` — Formular für anlegen + bearbeiten.
- `modules/users/UsersListView.test.ts` — Rendering/Gating-Test.
- Mod: `app/router.ts` (3 Routen), `app/AppShell.vue` (Sidebar-Link „Benutzer").

## Arbeitsschritte
1. Feature-Branch `feat/admin-users`.
2. `BackendPermissionKeys`-Werte verifizieren (user.*, role.*) für das Gating.
3. `usersApi.ts` + Tests (TDD: Test rot → Service → grün).
4. `UsersListView.vue` (Liste, Rolle verknüpft, Aktionen nach Permission) + Test.
5. `UserDetailView.vue` (anlegen/bearbeiten, Rolle-Dropdown wenn `role.*`).
6. Router + Sidebar-Link.
7. Verify: `npm run test` (Vitest) grün; `npm run build`; `dotnet build` der
   Solution 0/0; optional Boot + Klick-through gegen die laufende App.
8. Reviewer; Merge.

## Durchstich-Kriterium
Ein eingeloggter Operator öffnet „Benutzer", sieht die Liste mit Rollen, legt
einen neuen Benutzer mit Rolle an, bearbeitet und löscht ihn — alles gegen die
bestehenden Endpunkte, permission-gated. Vitest + Build grün.

## Risiken / Nicht-Ziele
- **Rollen-Definition** (Rollen anlegen, Permissions zuordnen) = späteres RBAC-
  Modul, nicht hier.
- **Passwort-Handling:** Update mit leerem Passwort darf es NICHT überschreiben —
  im Service/Backend-Vertrag prüfen (Update-Request Password optional).
- **Self-Lockout:** Operator entzieht sich selbst die Rolle / löscht sich selbst.
  Für den ersten Wurf: Backend-Verhalten übernehmen, im UI nicht sonderbehandeln;
  als Follow-up notieren, falls das Backend keinen Schutz hat.
