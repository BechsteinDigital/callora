# Retention & GDPR

Your plugin's data lives in a schema the host cannot reach. That isolation is a feature — but
it means that when a workspace is deleted and the platform must erase everything about it, the
host cannot delete *your* rows for you. If your plugin holds personal data, you must hand the
host a way to erase it. That is what `IWorkspaceDataPurgeContributor` is for.

This page explains the workspace-purge lifecycle, how you contribute your plugin's erasure
logic, how the host aggregates contributors, and where host-level data retention fits in. The
worked example is `CommunicationWorkspaceDataPurgeContributor`
(`custom/static-plugins/Communication/src/Application/Persistence/`).

## What you'll learn

- Why every plugin holding personal data must implement a purge contributor
- How to write and export an `IWorkspaceDataPurgeContributor`
- How the host aggregates contributors and treats failures as compliance events
- How host-level retention (jobs, notifications) already handles PII it owns

::: tip Prerequisites

- A plugin that stores **workspace-scoped** data — in [EF entities](./entities-and-schemas)
  or the [data store](./data-store) — with a `WorkspaceKey` on every relevant row/document.
- You can `context.Export(...)` from `StartAsync` — see
  [Exporting extensions](/guides/fundamentals/exporting-extensions).
:::

## Why you must contribute

When a workspace is purged (GDPR erasure, REV2 §14), the host deletes the host-owned data it
can see and commits that transaction. It **cannot** open your `plugin_<id>` schema — by
design, that schema is yours alone. So the host delegates: each plugin that stores workspace
data exports **one** contributor, and the host purge invokes them all after its own purge
commits.

If you skip this, your plugin's rows for a deleted workspace become **orphaned personal
data** — a compliance liability. Any plugin holding personal data for a workspace should ship
a contributor.

## The contract

`IWorkspaceDataPurgeContributor` (`src/Core/Application/Persistence/Contracts/`) is a single
method:

```csharp
[CalloraExtensible("Extension point — implement to purge plugin workspace data (REV2 §8.2)")]
public interface IWorkspaceDataPurgeContributor
{
    // Delete all data the plugin holds for the given workspace. Invoked after the
    // host purge has committed; a failure is logged, not fatal.
    Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default);
}
```

## Step 1 — Implement it

Erase every row keyed to that workspace, across every table (or collection) you own. From
`CommunicationWorkspaceDataPurgeContributor` — it deletes call logs and SIP accounts in the
`plugin_communication` schema:

```csharp
public sealed class CommunicationWorkspaceDataPurgeContributor(
    IPluginDbContextFactory<VoipDbContext> dbContextFactory) : IWorkspaceDataPurgeContributor
{
    public async Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken ct = default)
    {
        await using var db = dbContextFactory.CreateDbContext();

        await db.CallLogs
            .Where(callLog => callLog.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(ct);

        await db.SipAccounts
            .Where(sipAccount => sipAccount.WorkspaceKey == workspaceKey)
            .ExecuteDeleteAsync(ct);
    }
}
```

**Expected behavior:** after this runs, no row in your schema references that workspace key.
`ExecuteDeleteAsync` issues a set-based `DELETE`, so it's efficient even for large tables.

::: tip Cover every store
Delete from **all** tables and data-store collections that carry the workspace's data — call
logs *and* SIP accounts here. Miss one and you leave orphaned personal data. If you use the
[data store](./data-store), iterate `ListWorkspaceKeysAsync` / `RemoveAsync` (or list the
collection for that workspace and remove each entry).
:::

## Step 2 — Export it on activation

Export the contributor from `StartAsync`, once, so the host indexes it. From
`CommunicationPlugin.StartAsync`:

```csharp
context.Export<IWorkspaceDataPurgeContributor>(
    new CommunicationWorkspaceDataPurgeContributor(dbContextFactory));
```

::: info Export is hot
Exports are added on activation and dropped on deactivation. An active plugin's contributor
participates in every purge; a deactivated plugin's does not. There's nothing to register in
config — the export *is* the registration.
:::

## How the host aggregates contributors

`PluginWorkspaceDataPurger` (`src/Core/Application/Workspaces/`) resolves **every** exported
contributor from the plugin catalog and invokes each, best-effort:

```csharp
foreach (var contributor in catalog.GetExports<IWorkspaceDataPurgeContributor>())
{
    try
    {
        await contributor.PurgeWorkspaceAsync(workspaceKey, cancellationToken);
    }
    catch (Exception exception)
    {
        failures++;
        logger.LogError(exception,
            "COMPLIANCE: plugin workspace-data purge contributor {Contributor} failed for " +
            "workspace {WorkspaceKey}; plugin-owned rows remain and must be retried.",
            contributor.GetType().FullName, workspaceKey);
    }
}
```

Two things follow from this design:

- **Best-effort isolation.** One contributor throwing does **not** abort the others, and does
  not roll back the already-committed host purge. Your contributor runs regardless of what a
  sibling plugin does.
- **Failures are compliance events, not warnings.** A failure is logged at `Error` with a
  `COMPLIANCE:` prefix and counted; `PurgeAsync` returns the failure count. A non-zero count
  means some plugin's personal data may remain and the purge **must be retried**.

::: warning Make your purge idempotent and resilient
Because a failed purge is retried, `PurgeWorkspaceAsync` may run more than once for the same
workspace. Set-based deletes are naturally idempotent (a second run deletes zero rows).
Avoid throwing on "nothing to delete", and don't assume single execution.
:::

## Host-level retention (what you *don't* have to do)

The platform already ages out PII it owns itself. `RetentionCleanupJobHandler`
(`src/Core/Application/Retention/`) is a recurring sweep that deletes completed background
jobs and old notifications once their retention window elapses — because job payloads can
carry phone numbers and e-mail addresses (GDPR storage limitation, Art. 5(1)(e)). The windows
are configurable via `RetentionOptions`:

```csharp
public sealed class RetentionOptions
{
    public bool Enabled { get; set; } = true;
    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromHours(6);
    public TimeSpan CompletedJobRetention { get; set; } = TimeSpan.FromDays(14);
    public TimeSpan NotificationRetention { get; set; } = TimeSpan.FromDays(90);
}
```

This covers **host-owned** data (jobs, notifications). It does **not** touch your plugin's
schema — time-based retention of your own tables is your responsibility, and the
workspace-purge contributor above covers erasure on workspace deletion.

> **Status:** the platform ships an erasure hook (`IWorkspaceDataPurgeContributor`, invoked on
> workspace purge) and host-side time-based retention for jobs and notifications. A
> general-purpose **data export** contributor (subject-access / portability across plugins) is
> not part of the contracts surfaced here; if your compliance process needs export, build it
> in your plugin against your own schema for now.

## Next steps

- The data these contributors erase: **[Entities & schemas](./entities-and-schemas)** · **[Data store](./data-store)**
- How custom-field values are cleaned up (the host handles those): **[Custom fields](./custom-fields#cleanup-on-gdpr-purge)**
- Declaring compliance-relevant manifest fields: **[Compliance metadata](/guides/fundamentals/compliance-metadata)** · **[Registry manifest](/guides/fundamentals/registry-manifest)**
- How `context.Export(...)` works: **[Exporting extensions](/guides/fundamentals/exporting-extensions)**
- Contract signatures: **[.NET API](/api/)**
