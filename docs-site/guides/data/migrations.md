# Migrations

Your plugin's schema has to exist before you touch it, and it has to *stay* correct as your
plugin evolves across versions. Callora handles this with EF Core migrations that ship inside
your plugin assembly and run — under a database advisory lock — as the first thing your
plugin does on activation.

This page covers the primary path (EF migrations via `MigrateAsync`), when migrations run,
why the advisory lock matters, and a worked migration from the Communication plugin. It also
documents the lower-level `IPluginMigration` runner for plugins that prefer raw SQL.

## What you'll learn

- How to generate and ship EF Core migrations targeting your plugin as the migrations
  assembly
- How and when the host applies them (`MigrateAsync`, first in `StartAsync`)
- Why migration runs under a Postgres advisory lock, so concurrent hosts can't race
- The alternative `IPluginMigration` / `IPluginMigrationRunner` raw-SQL path

::: tip Prerequisites
- A `DbContext` pinned to your `plugin_<id>` schema — see
  [Entities & schemas](./entities-and-schemas).
- The EF Core tooling: `dotnet tool install --global dotnet-ef`.
- A design-time context factory so the CLI can build your context outside the host. The
  Communication plugin ships one as `VoipDbContextDesignTimeFactory`
  (`custom/static-plugins/Communication/src/Application/Persistence/`).
:::

## The model

`IPluginDbContextFactory<TContext>` exposes `MigrateAsync`:

```csharp
public interface IPluginDbContextFactory<TContext>
    where TContext : DbContext
{
    TContext CreateDbContext();
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
```

Under the hood (`PluginDbContextFactory<TContext>`, `src/Core/Application/Plugins/`),
`MigrateAsync`:

1. Opens the host database connection.
2. Takes a **Postgres advisory lock** derived from your `pluginId`
   (`SELECT pg_advisory_lock(<key>)`).
3. Runs `context.Database.MigrateAsync(...)` — applying every pending EF migration.
4. Releases the lock and closes the connection.

Because **your plugin assembly is EF's migrations assembly**, EF discovers the migration
classes you ship, not the host's.

## Step 1 — Generate a migration

Run `dotnet ef` against your plugin project, naming your context:

```bash
dotnet ef migrations add InitialVoipSchema \
  --project custom/static-plugins/Communication/Callora.Plugin.Communication.csproj \
  --context VoipDbContext
```

This writes a migration class plus a model snapshot into your project (the Communication
plugin keeps them under `.../Persistence/Migrations/`). Commit these files — they are part of
your plugin.

## Step 2 — Inspect the migration

A generated migration is a normal EF `Migration`. From `AddSipAccounts.cs`, note that the
schema name is baked in — every operation targets `plugin_communication`:

```csharp
public partial class AddSipAccounts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "sip_accounts",
            schema: "plugin_communication",
            columns: table => new
            {
                WorkspaceKey = table.Column<string>(maxLength: 120, nullable: false),
                SipAccountId = table.Column<string>(maxLength: 200, nullable: false),
                Username = table.Column<string>(maxLength: 200, nullable: false),
                ProtectedSecret = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
                table.PrimaryKey("PK_sip_accounts", x => new { x.WorkspaceKey, x.SipAccountId }));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "sip_accounts", schema: "plugin_communication");
}
```

## Step 3 — Run migrations on activation

Migrations run when your plugin **activates**, driven by your own `StartAsync`. Resolve the
factory and call `MigrateAsync` **before** any code reads or writes the schema:

```csharp
public async ValueTask StartAsync(IHostPluginContext context, CancellationToken ct = default)
{
    var factory = (IPluginDbContextFactory<VoipDbContext>)
        context.Services.GetService(typeof(IPluginDbContextFactory<VoipDbContext>))!;

    await factory.MigrateAsync(ct); // FIRST — schema exists before it is used

    // … build stores, export contributors, etc.
}
```

**Expected behavior:** on the first activation, pending migrations create your tables in
`plugin_<id>`. On later activations with nothing pending, `MigrateAsync` is a fast no-op.
Activation blocks until migration completes, so downstream code can assume the schema is
current.

::: warning Migrate first, always
`MigrateAsync` must be the first data-touching call in `StartAsync`. If you query before
migrating, the tables may not exist yet. The Communication plugin migrates, then imports
legacy data, then wires up its stores — in that order.
:::

## Why the advisory lock

In a scaled-out deployment several host instances can activate the same plugin at nearly the
same moment. Without coordination they would race to apply the same migration and could
deadlock or double-apply. The Postgres advisory lock keyed on your `pluginId` serializes
this: **exactly one instance migrates at a time**, the others wait, and each migration is
applied once. You get this for free — there is nothing to configure.

::: info Host tables migrate the same way
The host applies its own EF migrations under an advisory lock at startup
(`HostDatabaseInitializationHostedService`, `src/Core/Infrastructure/Persistence/`). Plugin
migrations use the identical pattern, keyed per plugin, so host and plugins never contend on
the same lock.
:::

## Alternative — raw-SQL migrations with `IPluginMigration`

If you'd rather run hand-written SQL than EF migrations, the platform exposes a lower-level
runner. You implement `IPluginMigration` (`src/Core/Application/Migrations/Contracts/`) per
change:

```csharp
[CalloraExtensible("Extension point — implement to define a plugin schema migration (REV2 §8.2)")]
public interface IPluginMigration
{
    int Version { get; }         // monotonically increasing: 1, 2, 3, …
    string Description { get; }  // short human-readable summary

    // Commands MUST enlist in the provided transaction.
    Task UpAsync(DbConnection connection, DbTransaction transaction, CancellationToken ct = default);
}
```

`IPluginMigrationRunner.RunAsync(pluginId, migrations, ct)` then applies every version not
yet recorded, **in version order, each in its own transaction**, and writes a bookkeeping row
per plugin+version into the host `plugin_migrations` table
(`ScopedPluginMigrationRunner`, `src/Core/Infrastructure/Plugins/`). Because schema change
and bookkeeping commit together, a crash mid-run leaves no half-applied version.

By convention, tables created this way use the `plugin_<pluginId>_*` prefix (logged for
audit).

::: info Which path should I use?
Prefer **EF migrations + `MigrateAsync`** — it's the path the first-party Communication
plugin uses, gives you the snapshot/diff tooling, and matches how you already model entities.
Reach for `IPluginMigration` only when you genuinely need raw SQL the EF migration builder
can't express.
:::

## Next steps

- Define the entities these migrations create: **[Entities & schemas](./entities-and-schemas)**
- Erase workspace data when a workspace is purged: **[Retention & GDPR](./retention-and-gdpr)**
- Declare `databaseSchema` so uninstall drops your schema: **[Registry manifest](/guides/fundamentals/registry-manifest)**
- Contract signatures: **[.NET API](/api/)**
