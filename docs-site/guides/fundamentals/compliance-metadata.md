# Compliance Metadata

Plugins that touch personal data — phone numbers, e-mail addresses, caller identities —
carry compliance obligations. Callora gives you three concrete, implemented mechanisms:
declaring which payload fields are **sensitive**, an isolated **database schema** per
plugin, and a **workspace purge contributor** for GDPR erasure. This page covers what each
one does, how it's wired, and where the manifest data is actually read.

## What you'll learn

- Declaring `sensitiveFields` for webhook data minimization
- How `databaseSchema` and the `plugin_<id>` convention isolate your data
- Implementing `IWorkspaceDataPurgeContributor` for GDPR erasure
- Which manifest fields are read by which service (an accuracy note that matters)

::: warning
**Accuracy note.** `databaseSchema` and `sensitiveFields` are **not** part of the core
`PluginRegistryJsonDto` — the minimal DTO the loader reads for identity and lifecycle. They
are read by dedicated services: `databaseSchema` by `PluginManifestSchemaReader`,
`sensitiveFields` by `RegistrySensitiveFieldSyncService`. So they live in your
`registry.json` alongside the core fields, but they're consumed by separate subsystems, not
the plugin loader.
:::

## `sensitiveFields`: data minimization for webhooks

When your plugin's data leaves the platform through a webhook, person-related fields should
not go out in the clear. You declare those field names in `registry.json`; the platform
masks them before dispatch. From the Communication plugin's `registry.json`:

```json
{
  "pluginId": "communication",
  "sensitiveFields": [
    "phoneNumber",
    "callerNumber",
    "calleeNumber"
  ]
}
```

At install, `RegistrySensitiveFieldSyncService`
(`src/Core/Infrastructure/Webhooks/RegistrySensitiveFieldSyncService.cs`) reads this array
and registers the names into `SensitivePayloadFieldRegistry`. That registry already holds a
domain-neutral core baseline (`target`, `targetValue`, `targetDisplayName`, `displayName`,
`email`); your declared fields are added on top, keyed by plugin.

At dispatch, `WebhookPayloadMinimizer` walks the payload recursively and masks any matching
field: values longer than five characters become `first3***last2`; shorter ones become
`***`. Nested objects and arrays are traversed too, so a `phoneNumber` buried inside a call
record is still masked.

::: info
`sensitiveFields` controls **webhook payload masking in transit**. It is separate from
`IPluginDataProtector` (`Protect`/`TryUnprotect`,
`src/Core/Application/Secrets/Contracts/IPluginDataProtector.cs`), which **encrypts values
at rest** using per-plugin isolated purposes. Use `sensitiveFields` to keep person data out
of outbound webhooks; use `IPluginDataProtector` to encrypt sensitive values you store.
:::

## `databaseSchema` and `plugin_<id>` isolation

Each plugin that persists data gets its **own Postgres schema**, so plugins can't read or
clobber each other's tables. By convention the schema is `plugin_<id>` — the plugin id,
lowercased, hyphens turned to underscores (`PluginSchemaName`,
`src/Core/Infrastructure/Persistence/PluginSchemaName.cs`). The Communication plugin's
manifest pins it explicitly:

```json
{
  "pluginId": "communication",
  "databaseSchema": "plugin_communication"
}
```

The `databaseSchema` field is optional — it's an **override** read by
`PluginManifestSchemaReader`. If you omit it, the `plugin_<id>` convention applies. Your EF
Core `DbContext` sets it as the default schema:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema("plugin_communication");
    // ... entities ...
}
```

Because the host can't reach into your schema, **cleanup is your responsibility**. On
uninstall the host drops the correct schema (`DROP SCHEMA IF EXISTS … CASCADE`) using the
sanitized name — but *workspace-scoped* deletion within a live plugin is handled by a purge
contributor, below.

::: warning
Whatever you set in `databaseSchema` is sanitized before use, but keep it aligned with the
`plugin_<id>` convention (`plugin_yourid`). A mismatched or reused schema name risks name
collisions with another plugin and complicates the uninstall drop.
:::

## GDPR erasure: `IWorkspaceDataPurgeContributor`

When a workspace is purged (a GDPR erasure request, an offboarded customer), the host
commits its own deletion first — then asks every plugin to erase *its* workspace-scoped
rows, because it can't reach your isolated schema. You implement and **export** one
contributor (`src/Core/Application/Persistence/Contracts/IWorkspaceDataPurgeContributor.cs`):

```csharp
public interface IWorkspaceDataPurgeContributor
{
    // Deletes all data the plugin holds for the given workspace.
    // Invoked after the host purge has committed; a failure is logged, not fatal.
    Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
```

Export it from `StartAsync` like any other contract:

```csharp
context.Export<IWorkspaceDataPurgeContributor>(
    new CommunicationWorkspaceDataPurgeContributor(dbContextFactory));
```

The host collects every contributor and runs them one by one
(`PluginWorkspaceDataPurger`):

```csharp
foreach (var contributor in catalog.GetExports<IWorkspaceDataPurgeContributor>())
{
    try { await contributor.PurgeWorkspaceAsync(workspaceKey, cancellationToken); }
    catch (Exception exception)
    {
        failures++;
        logger.LogError(exception,
            "COMPLIANCE: plugin workspace-data purge contributor {Contributor} failed for {WorkspaceKey}; " +
            "plugin-owned rows remain and must be retried.",
            contributor.GetType().FullName, workspaceKey);
    }
}
```

**Expected behavior:** each contributor runs independently and best-effort. One failing
contributor is logged and does **not** block the others or roll back the committed host
purge — but a non-zero failure count means some plugin-owned rows remain and the purge must
be retried. Your `PurgeWorkspaceAsync` should therefore be **idempotent**: safe to run
again after a partial failure.

::: tip
Delete by `workspaceKey`, scope the delete tightly, and make it re-runnable. Because purge
is retried on failure, a contributor that deletes the same rows twice with no error is
exactly what compliance needs.
:::

## What's implemented today

Callora's compliance manifest surface is deliberately narrow and concrete:

| Mechanism | Manifest field / contract | Read by |
| --- | --- | --- |
| Webhook data minimization | `sensitiveFields` | `RegistrySensitiveFieldSyncService` |
| Schema isolation | `databaseSchema` (optional) / `plugin_<id>` convention | `PluginManifestSchemaReader` / `PluginSchemaName` |
| Encryption at rest | `IPluginDataProtector` | host data-protection service |
| GDPR erasure | `IWorkspaceDataPurgeContributor` (exported) | `PluginWorkspaceDataPurger` |

> **Status:** A broader "compliance schema" — declarative data-retention policies, consent
> or data-category metadata, structured GDPR export descriptors — is **planned**, not
> implemented. Today, compliance metadata is exactly the three mechanisms above:
> `sensitiveFields` + `databaseSchema` + the purge contributor. No other compliance fields
> are parsed from `registry.json`.

## Next steps

- Exporting the purge contributor: **[Exporting extensions](./exporting-extensions)**
- Secrets and encryption: **[Plugin configuration](./plugin-configuration#defining-and-setting-config-operator-endpoints)** · **[Best Practices](./best-practices#keep-secrets-in-isecretstore)**
- Your database schema and manifest fields: **[The registry manifest](./registry-manifest)** · **[Extension manifests reference](/reference/extension-manifests)**
- Data-handling checklist: **[Best Practices](./best-practices)**
