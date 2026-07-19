# Migration & Rollback

## The database is the source of truth

Callora keeps **plugin lifecycle state in the database**, not on the filesystem. A
plugin is `installed`, `active`, or removed because a row in the host database says
so — discovery on the filesystem is a one-shot input, not the truth. This is why
install/activate/deactivate/uninstall survive restarts and why rollback is a
database operation, not a redeploy.

## Host migrations

Host schema changes live **exclusively in EF Core migrations** under
`src/Core/Infrastructure/Persistence/Migrations` (the former inline DDL is gone).

Migrations are applied **automatically at startup** by
`HostDatabaseInitializationHostedService`:

1. Open the DB connection and take a **Postgres advisory lock**
   (`pg_advisory_lock`) — this makes multi-instance startup safe: only one
   instance migrates at a time, the rest wait.
2. Apply pending migrations (`ApplyMigrationsAsync`).
3. Release the advisory lock.
4. Import any legacy filesystem data-protection keys into the DB keyring, ensure
   the default tenant exists, then run the RBAC seeder.

### Authoring a host migration

```bash
dotnet ef migrations add <Name> \
  --project src/Core/Callora.Core.csproj \
  --context HostPersistenceDbContext
```

Review the generated migration, keep it forward-only where possible, and let the
app apply it at startup. Do not hand-write DDL outside migrations.

## The admin→superadmin seed

On startup the `BackendRbacDatabaseSeeder` establishes the RBAC baseline:

- A **`SuperAdmin`** system role with the wildcard permission `*` (global
  operator). This role is idempotently created and marked `IsSystem=true`; if it
  already exists, the seeder re-asserts the system flag and the `*` grant.
- A migration (`MigrateAdminRoleToSuperAdmin`) moves the historical `admin` role
  to `SuperAdmin` — `admin` is **no longer** a full operator. In the current RBAC
  model, `SuperAdmin` is global and `Admin` is scoped **per workspace**.
- A **bootstrap operator** password policy: an initial operator password shorter
  than **12 characters is refused, not weakened**.

## Per-plugin migrations

Each plugin **carries its own EF Core migrations** and owns an isolated
`plugin_<id>` PostgreSQL schema on the shared host database. For the Communication
plugin, the `VoipDbContext` sets `HasDefaultSchema("plugin_communication")` and
ships migrations under
`custom/static-plugins/Communication/src/Application/Persistence/Migrations`.

Plugin migrations run on the plugin's own lifecycle path — the plugin calls
`MigrateAsync` when it is brought up — so a plugin's schema is created/updated when
it is installed/activated, and its data stays quarantined in its schema.

## Plugin install / activate / rollback

Lifecycle is driven through the control-plane API and reflected in the database.

- **install** — register the plugin (subject to the signature/trust gate — see
  [Security](security.md)). Example against the local dev key:

  ```bash
  curl -s -X POST http://localhost:5000/api/plugins/install \
    -H "X-Callora-Api-Key: callora-local-dev-key-change-me" \
    -H "Content-Type: application/json" \
    -d '{"assemblyPath":"/abs/path/to/Callora.Plugin.Communication.dll"}'
  ```

- **activate / deactivate** — flip the plugin's active state. Activation passes
  hard gates first: contract compatibility, package signature/trust, tenant
  entitlement, and compliance metadata. **Deactivation must stop the plugin's data
  flow immediately** and wind down running jobs in a controlled way (compliance
  requirement).
- **uninstall** — remove the installation. A workspace-scoped data purge
  contributor erases the plugin's workspace data in its `plugin_<id>` schema.

Install/activate happen **hot** via the operator API — no host restart needed.
The `install/nuget` path resolves a plugin from the local NuGet feed.

## Safe rollback practices

- **Prefer forward-only migrations.** For a host schema regression, ship a new
  corrective migration rather than reverting in place; the advisory-lock startup
  path applies it safely across instances.
- **Roll back a bad plugin via the lifecycle, not the filesystem.** Deactivate
  (stops data flow) or uninstall through the API; the DB state is authoritative,
  so the change persists across restarts.
- **Revoke a bad build** at the trust layer: add its content hash to
  `BackendHost__RevokedContentHashes` (or the signer to
  `BackendHost__RevokedSignerFingerprints`). Revocation is enforced both at install
  and — through runtime rehydration — at load, so an already-installed bad build
  will not reload.
- **Workspace template rollback** has its own scoped procedure and endpoints — see
  the [Runbooks](runbooks.md#workspace-template-rollout).
- **Back up the database before a risky migration or bulk lifecycle change.** All
  state — host schema, every plugin schema, RBAC, and the data-protection keyring —
  lives in the one PostgreSQL database, so a database snapshot is a complete
  rollback point.

> **Status:** EF migrations, the advisory-lock startup path, the RBAC seed, and
> per-plugin schema migrations are implemented. A generalized "safe rollback for
> plugins" workflow with automated snapshot/restore is tracked as a platform item
> (PLAT-062) and is not yet a turnkey command.
