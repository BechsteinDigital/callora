# Data Handling

A Callora plugin owns its data. When it needs to persist anything — call logs, SIP
accounts, per-workspace settings, an API key — it does so in storage the host gives it,
isolated from the host tables and from every other plugin. Nothing you store leaks into
another plugin's world, and when your plugin is uninstalled the host can drop it all
cleanly.

This section is the practical guide to that model: how you model entities, migrate schema,
extend host records, stash key/value documents, protect secrets, and stay compliant when a
workspace is erased.

## What you'll learn

- How the platform isolates each plugin's data in a dedicated `plugin_<id>` Postgres schema
- The building blocks — EF entities, migrations, custom fields, the data store, secrets, and
  retention — and when to reach for each
- A recommended learning path, from "store a row" to "erase a workspace"

## How a plugin owns data

Callora is database-backed. Instead of dumping everything into shared JSON columns, a plugin
models its data as **real typed EF Core entities in its own Postgres schema**, named
`plugin_<id>` after the `pluginId` in its [`registry.json`](/guides/fundamentals/registry-manifest).
The Communication plugin, for example, keeps every table in `plugin_communication`:

```csharp
public sealed class VoipDbContext(DbContextOptions<VoipDbContext> options) : DbContext(options)
{
    public const string SchemaName = "plugin_communication";

    public DbSet<CallLog> CallLogs => Set<CallLog>();
    public DbSet<SipAccount> SipAccounts => Set<SipAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName); // all tables land in plugin_communication
        // entity configuration …
    }
}
```

The host supplies a connection-bound `IPluginDbContextFactory<TContext>` that points EF at
the shared host database with your assembly as the migrations assembly. You never touch a
connection string, and you can never read into `plugin_communication` from a different
plugin.

::: info The host cannot reach into your schema either
Because your schema is yours alone, the host **cannot** delete your workspace-scoped data
during a GDPR purge. That is exactly why the [retention & GDPR](./retention-and-gdpr) page
exists: you export a contributor and the host calls it. Isolation cuts both ways.
:::

## The pieces

| Concern | Contract | Page |
| --- | --- | --- |
| Model your own tables | `IPluginDbContextFactory<TContext>` | [Entities & schemas](./entities-and-schemas) |
| Create/upgrade schema | `MigrateAsync` · `IPluginMigration` | [Migrations](./migrations) |
| Extend host/other entities | `ICustomFieldAccessor` · `/api/custom-fields` | [Custom fields](./custom-fields) |
| Simple key/value documents | `IPluginDataStore` · `IPluginDataProtector` | [Data store](./data-store) |
| Erase / export on purge | `IWorkspaceDataPurgeContributor` | [Retention & GDPR](./retention-and-gdpr) |
| Credentials & API keys | `ISecretStore` | [Secrets](./secrets) |

## Which storage should I use?

- **A record with structure, queries, indexes, or relations** → define EF entities in your
  own schema. Start at [Entities & schemas](./entities-and-schemas).
- **A handful of JSON documents keyed by id** (config, small per-workspace state) → the
  general-purpose [data store](./data-store). No migrations to ship.
- **An extra field on a *host* record** (a workspace, a user) rather than a record of your
  own → [custom fields](./custom-fields).
- **A password, token, or API key** → never a plain column; use encryption at rest via
  `IPluginDataProtector` (for values you store) or read operator-provided credentials via
  [`ISecretStore`](./secrets).

## Learning path

1. **[Entities & schemas](./entities-and-schemas)** — define a `DbContext` and your first
   entity in `plugin_<id>`, and get a factory from the curated container.
2. **[Migrations](./migrations)** — ship EF migrations and run them from `StartAsync` under
   an advisory lock so the schema exists before you use it.
3. **[Data store](./data-store)** — the lightweight key/value alternative, plus encrypting
   sensitive values with `IPluginDataProtector`.
4. **[Custom fields](./custom-fields)** — attach fields to host entities and read/write them
   through the operator API.
5. **[Secrets](./secrets)** — resolve operator-provided credentials with `ISecretStore`.
6. **[Retention & GDPR](./retention-and-gdpr)** — contribute your data to workspace export
   and erasure so the platform stays compliant.

## Next steps

- Start modelling: **[Entities & schemas](./entities-and-schemas)**
- How you attach any of this from `StartAsync`: **[Exporting extensions](/guides/fundamentals/exporting-extensions)**
- The `databaseSchema` / `sensitiveFields` manifest fields: **[Registry manifest](/guides/fundamentals/registry-manifest)**
- The bigger picture: **[Architecture](/concepts/architecture)**
