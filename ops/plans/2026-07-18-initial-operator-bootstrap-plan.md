# Initial-Operator-Bootstrap (Plan)

Datum: 2026-07-18 · Core/Backend. Repo: callora. Löst den in
[[callora-admin-shell-colocated-2026-07]] notierten Bootstrap-Gap: der erste
Operator in Prod (DemoAdmin dort disabled) ist nicht über die UI anlegbar.

## Ziel

Ein frisch deployter Prod-Host seedet beim ersten Start genau einen Operator
(SuperAdmin) aus der Config/`.env`, **nur wenn noch kein Benutzer existiert** —
danach verwaltet der Betreiber alles über die Admin-Shell.

## Ist

`BackendRbacDatabaseSeeder` (aufgerufen von `HostDatabaseInitializationHostedService`)
ist bereits ein Operator-Seed: `EnsureDemoAdminUserAsync` legt einen User an und
weist die SuperAdmin-Rolle zu. Aber: (a) DemoAdmin trägt den Dev-Default (Guard
blockt Prod), (b) es upsertet bei JEDEM Start (überschreibt spätere UI-Änderungen).

## Kernentscheidungen (DECISION-Log)

1. **Separater `InitialOperator`-Seed statt DemoAdmin umwidmen.** DemoAdmin bleibt
   Dev-Convenience (immer re-seed); InitialOperator ist der Prod-Bootstrap.
   Semantisch ehrlich (kein „Demo" in Prod).
2. **Idempotent — nur wenn die Benutzer-Tabelle leer ist.** Kein Überschreiben:
   nach dem ersten Login ändert der Betreiber Passwort/Details über die UI, ein
   Neustart setzt nichts zurück. Prüfung deckt lokalen Context + DB ab (robust
   gegen die Seed-Reihenfolge).
3. **Gemeinsame Upsert-Logik extrahiert** (`UpsertOperatorAsync`): DemoAdmin und
   InitialOperator teilen das Anlegen-und-Rolle-zuweisen (DRY).
4. **Kein neuer Guard-Zweig:** InitialOperator hat keinen repo-bekannten Default
   (Password default leer → Seed greift nicht). Credentials kommen aus `.env`;
   ein schwaches Passwort ist Betreiber-Verantwortung, kein repo-Default.

## Dateien
- `src/Core/Application/Policies/BackendInitialOperatorOptions.cs` (neu): Enabled,
  ExternalId, Email, DisplayName, Password.
- `src/Core/Application/Policies/BackendHostOptions.cs` (mod): `InitialOperator`.
- `src/Core/Infrastructure/Persistence/BackendRbacDatabaseSeeder.cs` (mod):
  `UpsertOperatorAsync` extrahiert; `EnsureInitialOperatorAsync` (nur bei leerer
  User-Tabelle); in `SeedAsync` nach DemoAdmin aufgerufen.
- `src/Core/PublicAPI.Unshipped.txt` (mod): neuer public Typ.
- `tests/Callora.Core.Tests/Infrastructure/Persistence/BackendRbacDatabaseSeederTests.cs`
  (neu): Testcontainers-Postgres, `[SkippableFact]` — seedet bei leerer DB,
  seedet NICHT bei vorhandenem User, disabled seedet nichts.

## Arbeitsschritte
1. Feature-Branch `feat/initial-operator`.
2. Options + BackendHostOptions.
3. Seeder: Upsert extrahieren, EnsureInitialOperatorAsync, verdrahten.
4. Baseline aktualisieren (RS0016 → neuer Symbol-Eintrag in-place anhängen).
5. Test (Testcontainers).
6. `dotnet build` 0/0; `dotnet test` (Seeder-Tests laufen mit Docker, sonst
   Skip; restliche Suite grün).
7. Reviewer; Merge.

## Config-Verdrahtung (Callora-Production — FOLGE-Schritt, eigener Zyklus)
`.env.example` (`CALLORA_INITIAL_OPERATOR_EXTERNALID`/`_PASSWORD`), compose
(`BackendHost__InitialOperator__*`), README-Notiz. Braucht Re-Pack + sync-packages.
Nicht Teil dieses Core-Bausteins.

## Durchstich-Kriterium
Auf leerer DB legt der Seeder mit aktivem InitialOperator einen SuperAdmin-User
an; auf einer DB mit bereits ≥1 Benutzer bleibt er inaktiv. Suite grün.

## Nicht-Ziele / Risiken
- Kein Multi-Operator-Seed (genau einer).
- Reihenfolge DemoAdmin→InitialOperator: in der Praxis nie gleichzeitig aktiv
  (Dev: DemoAdmin an / InitialOperator aus; Prod umgekehrt); Local+DB-Prüfung
  macht es dennoch robust.
