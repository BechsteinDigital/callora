# Entities & Schemas

Callora is database-backed. When your plugin needs to persist structured data — records with
columns, indexes, and relations — you model it as **real EF Core entities in your own
Postgres schema**, not as loose JSON. The host hands you a connection-bound factory; you own
the `DbContext`, the entities, and the LINQ.

This page walks the full task: define a `DbContext`, pin it to your `plugin_<id>` schema,
define an entity, obtain the factory in `StartAsync`, and read and write rows. The worked
example is the Communication plugin's `VoipDbContext`
(`custom/static-plugins/Communication/src/Application/Persistence/`).

## What you'll learn

- Why each plugin gets an isolated `plugin_<id>` schema, and how the host enforces it
- How to define a `DbContext` bound to that schema and configure entities
- How to obtain an `IPluginDbContextFactory<TContext>` from the curated service provider
- How to read and write rows with plain LINQ

::: tip Prerequisites

- A working plugin with a `StartAsync(IHostPluginContext context, …)` entry point — see
  [Your first plugin](/guides/getting-started/your-first-plugin) and
  [Plugin entry](/guides/fundamentals/plugin-entry).
- Your `pluginId` from [`registry.json`](/guides/fundamentals/registry-manifest). Your schema
  name is `plugin_<pluginId>`.
- A reference to `Microsoft.EntityFrameworkCore` in your plugin project.
:::

## The isolation model

A plugin owns its data in a **dedicated Postgres schema** named `plugin_<id>` on the shared
host database. Tables in `plugin_communication` are invisible to `plugin_dialer` and to the
host's own tables. The contract that expresses this is
`IPluginDbContextFactory<TContext>` (`src/Core/Application/Persistence/Contracts/`):

```csharp
public interface IPluginDbContextFactory<TContext>
    where TContext : DbContext
{
    // One context instance bound to the host database. Callers dispose it.
    TContext CreateDbContext();

    // Apply pending EF migrations under a Postgres advisory lock.
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
```

The host resolves this factory into your plugin's **curated** service provider and points EF
at the shared host database, with **your plugin assembly as the migrations assembly**. You
never see a connection string, and there is no way to address another plugin's schema.

::: info Why a real schema, not a JSON blob
Real entities give you typed columns, indexes, foreign keys, and LINQ queries — and let the
host drop your schema cleanly on uninstall. For a handful of small documents where none of
that matters, the lighter [data store](./data-store) may be a better fit.
:::

## Step 1 — Define the DbContext

Your context is an ordinary EF Core `DbContext` with two rules:

1. Its constructor **must** take `DbContextOptions<TContext>` (the factory constructs it via
   that constructor).
2. It **must** pin its schema in `OnModelCreating` with
   `modelBuilder.HasDefaultSchema("plugin_<id>")`.

From `VoipDbContext.cs`:

```csharp
using Microsoft.EntityFrameworkCore;

namespace Callora.Plugin.Communication.Application.Persistence;

public sealed class VoipDbContext(DbContextOptions<VoipDbContext> options) : DbContext(options)
{
    public const string SchemaName = "plugin_communication";

    public DbSet<CallLog> CallLogs => Set<CallLog>();
    public DbSet<SipAccount> SipAccounts => Set<SipAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName); // every table lands in plugin_communication

        modelBuilder.Entity<CallLog>(entity =>
        {
            entity.ToTable("call_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WorkspaceKey).HasMaxLength(120).IsRequired();
            entity.Property(x => x.CallId).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.WorkspaceKey, x.StartedAtUtc });
        });

        modelBuilder.Entity<SipAccount>(entity =>
        {
            entity.ToTable("sip_accounts");
            entity.HasKey(x => new { x.WorkspaceKey, x.SipAccountId }); // composite key
            entity.Property(x => x.ProtectedSecret).IsRequired();       // encrypted at rest
        });
    }
}
```

**Expected behavior:** every `DbSet` on this context creates its table inside
`plugin_communication`. `SELECT * FROM plugin_communication.call_logs` works; the same table
name in another schema is a different table.

::: warning Match your schema name to your manifest
Declare the same schema in your [`registry.json`](/guides/fundamentals/registry-manifest) via
`"databaseSchema": "plugin_communication"`. That is what lets the host **drop your schema on
uninstall**. If the manifest omits it, your tables can be orphaned.
:::

## Step 2 — Define an entity

Entities are plain C# classes — no base class, no attributes required. Keep them small and
typed. From `CallLog.cs`:

```csharp
namespace Callora.Plugin.Communication.Application.Persistence;

public sealed class CallLog
{
    public Guid Id { get; set; }
    public string WorkspaceKey { get; set; } = string.Empty; // scope every row to a workspace
    public string CallId { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public string? TargetDisplayName { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }
}
```

::: tip Always carry a `WorkspaceKey`
If a row belongs to a workspace, store its `WorkspaceKey`. That single column is what lets
you filter per workspace *and* erase per workspace when a workspace is purged — see
[Retention & GDPR](./retention-and-gdpr). Rows with no workspace scope are operator-level
data.
:::

## Step 3 — Get the factory and migrate on start

Resolve `IPluginDbContextFactory<TContext>` from `context.Services` in `StartAsync`, and call
`MigrateAsync` **first** so your schema exists before anything uses it. From
`CommunicationPlugin.StartAsync`:

```csharp
public async ValueTask StartAsync(IHostPluginContext context, CancellationToken ct = default)
{
    var dbContextFactory = context.Services
        .GetService(typeof(IPluginDbContextFactory<VoipDbContext>))
        as IPluginDbContextFactory<VoipDbContext>;

    if (dbContextFactory is not null)
    {
        await dbContextFactory.MigrateAsync(ct); // create/upgrade the schema first

        // … now build stores that use the factory
    }
}
```

::: info The factory may be absent
`context.Services.GetService(...)` can return `null` when the host runs without a database
provider (for example in a lightweight test host). The Communication plugin treats the
factory as optional and falls back to the [data store](./data-store). Whether you need that
fallback depends on your plugin; resolve defensively if you do.
:::

## Step 4 — Read and write with LINQ

Once migrated, you create a context per unit of work, use it, and dispose it. **Callers own
the instance** — always `await using`. From `EfSipAccountStore.cs`:

```csharp
public async Task<IReadOnlyList<SipAccountEntry>> ListAsync(
    string workspaceKey,
    CancellationToken cancellationToken = default)
{
    var key = workspaceKey.Trim();
    await using var db = dbContextFactory.CreateDbContext();
    var rows = await db.SipAccounts
        .AsNoTracking()
        .Where(x => x.WorkspaceKey == key)   // always filter by workspace
        .OrderBy(x => x.SipAccountId)
        .ToListAsync(cancellationToken);

    return rows.Select(ToEntry).ToArray();
}
```

Writes are ordinary EF too — add or mutate a tracked entity, then `SaveChangesAsync`:

```csharp
await using var db = dbContextFactory.CreateDbContext();
db.SipAccounts.Add(new SipAccount
{
    WorkspaceKey = key,
    SipAccountId = id,
    Username = request.Username.Trim(),
    ProtectedSecret = dataProtector.Protect(CommunicationPlugin.Id, request.Secret), // never plaintext
    CreatedAtUtc = DateTimeOffset.UtcNow,
    UpdatedAtUtc = DateTimeOffset.UtcNow
});
await db.SaveChangesAsync(cancellationToken);
```

For bulk deletes (e.g. purging a workspace) use `ExecuteDeleteAsync`, which skips change
tracking:

```csharp
await db.SipAccounts
    .Where(x => x.WorkspaceKey == key && x.SipAccountId == id)
    .ExecuteDeleteAsync(cancellationToken);
```

::: warning Store secrets encrypted
Never persist a raw password or token in a column. In the example above the SIP secret is
run through `IPluginDataProtector.Protect(...)` before it hits `ProtectedSecret`, and
`TryUnprotect(...)` when read back. See [Data store](./data-store#protecting-sensitive-values)
and [Secrets](./secrets).
:::

## Next steps

- Create the tables behind this context: **[Migrations](./migrations)**
- Erase these rows when a workspace is purged: **[Retention & GDPR](./retention-and-gdpr)**
- The lighter key/value alternative: **[Data store](./data-store)**
- How `context.Export(...)` attaches your stores: **[Exporting extensions](/guides/fundamentals/exporting-extensions)**
- The manifest `databaseSchema` field: **[Registry manifest](/guides/fundamentals/registry-manifest)**
- Where this fits in the whole extension model: **[Backend extensions](/guides/backend-extensions)** · **[.NET API](/api/)**
