# Admin-Shell: RBAC-Rollen-Definition (Plan)

Datum: 2026-07-18 · Task #30, zweites Admin-Feature-Modul (nach Benutzer-Verwaltung).
Repo: callora, `src/Administration/Resources/app/administration`.

## Ziel

Ein Operator definiert Rollen und ordnet ihnen Permissions zu — der Gegenpart zur
Benutzer-Verwaltung (dort wird die Rolle einem Benutzer zugewiesen; hier wird die
Rolle mit Rechten befüllt). Komplettiert den Operator/RBAC-Bereich.

## Backend (existiert, wird konsumiert)

`/api/security/rbac` (gated `role.*`): GET `/roles` → `(Role, Permissions[])`;
GET `/permissions` → `(PermissionKey, Function, Action)` (alle verfügbaren);
PUT `/roles/{role}` `(Functions[{Function, Actions[]}])`; DELETE `/roles/{role}`.
Die `superadmin`-Rolle ist system/fixed → PUT/DELETE darauf wirft (RoleFixed).

## Kernentscheidungen (DECISION-Log)

1. **Geteilte API-Helfer nach `core/http.ts`:** `unwrap` (Problem-Details → Error)
   und `jsonInit` aus `usersApi.ts` dorthin extrahieren; `usersApi` + neuer
   `rolesApi` nutzen sie (DRY über die Feature-Module).
2. **Permission-Matrix als UI:** `GET /permissions` nach `Function` gruppieren,
   pro Action eine Checkbox; die Rolle trägt die angekreuzten `function.action`.
   Beim Speichern zurück in `Functions[{Function, Actions[]}]` gruppieren.
3. **System-Rolle schützen:** `superadmin` (Wildcard `*`) ist read-only —
   Bearbeiten/Löschen ausgeblendet. Erkennung über den bekannten Namen (wie das
   `*`-Gating in `permissions.ts`); kein Backend-`IsSystem`-Flag anfassen.
4. **Gating:** Liste an `role.read`, anlegen/bearbeiten/löschen an `role.update`
   (spiegelt die Backend-Gates), via `hasPermission`.

## Dateien
- `core/http.ts` (mod): `unwrap` + `jsonInit` exportiert.
- `modules/users/usersApi.ts` (mod): nutzt die geteilten Helfer.
- `modules/roles/rolesApi.ts` (+ `.test.ts`): Service (list, listPermissions,
  upsert mit Gruppierung, remove).
- `modules/roles/RolesListView.vue` (+ `.test.ts`): Liste + Aktionen (System-Schutz).
- `modules/roles/RoleDetailView.vue`: Name + Permission-Matrix (anlegen/bearbeiten).
- `app/router.ts` (mod): `/roles`, `/roles/new`, `/roles/:role`.
- `app/AppShell.vue` (mod): Sidebar-Link „Rollen".

## Arbeitsschritte
1. Branch `feat/admin-roles`.
2. Helfer nach http.ts; usersApi umstellen; Vitest grün (Regression).
3. rolesApi + Test (Gruppierungs-Payload, Fehlerpfad).
4. RolesListView + Test (Gating, System-Rolle read-only).
5. RoleDetailView (Matrix, anlegen/bearbeiten).
6. Router + Sidebar.
7. Verify: Vitest; `dotnet build` (vue-tsc) 0/0; volle Suite.
8. Reviewer; Merge.

## Durchstich-Kriterium
Ein Operator öffnet „Rollen", legt eine Rolle mit ausgewählten Permissions an,
bearbeitet und löscht sie; `superadmin` bleibt read-only. Vitest + Build grün.

## Nicht-Ziele
- Kein Backend-`IsSystem`-Flag (Frontend erkennt `superadmin`).
- Keine Rollen-Zuweisung an Benutzer (das ist die Benutzer-Verwaltung).
