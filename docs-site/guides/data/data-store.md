# Data Store

Not every plugin needs a schema. When you have a handful of JSON documents keyed by id —
per-workspace settings, a small list of configured accounts, cached state — the
`IPluginDataStore` is the lightweight path: no migrations, no `DbContext`, just get/set/list
by key. Every document is automatically scoped to your plugin, so nothing leaks.

This page covers the key/value store and its companion `IPluginDataProtector`, which encrypts
sensitive values at rest. The worked example is the Communication plugin's
`DataStoreSipAccountStore`
(`custom/static-plugins/Communication/src/Application/Accounts/`).

## What you'll learn

- How to address, read, write, list, and remove documents with `IPluginDataStore`
- How documents are scoped by plugin, workspace, and collection
- How to encrypt sensitive values at rest with `IPluginDataProtector`
- When to prefer the data store over EF entities

::: tip Prerequisites
- A running plugin with `context.Services` access — see [Plugin entry](/guides/fundamentals/plugin-entry).
- Your `pluginId` (from [`registry.json`](/guides/fundamentals/registry-manifest)) — every key
  starts with it.
:::

## Addressing a document

A document is addressed by a `PluginDataKey`
(`src/Core/Application/Data/Contracts/`) — four parts:

```csharp
public sealed record PluginDataKey(
    string PluginId,      // your plugin id
    string? WorkspaceKey, // workspace scope, or null for plugin-global data
    string Collection,    // logical collection, e.g. "sip-accounts"
    string EntryKey);     // id unique within the collection
```

A `PluginDataCollectionKey` is the same minus the entry key, used for listing.

::: info Scoping is enforced for you
The store is scoped by plugin. A key you build with your `PluginId` can only ever address
your data — you cannot read another plugin's documents even if you guess its collection
names. Pass `WorkspaceKey: null` for plugin-global data; pass a workspace key for
workspace-scoped data.
:::

## The interface

`IPluginDataStore` (`src/Core/Application/Data/Contracts/`), resolvable from
`context.Services`:

```csharp
public interface IPluginDataStore
{
    Task<string?> GetAsync(PluginDataKey key, CancellationToken ct = default);
    Task SetAsync(PluginDataKey key, string jsonDocument, CancellationToken ct = default);
    Task<bool> RemoveAsync(PluginDataKey key, CancellationToken ct = default);

    Task<IReadOnlyList<PluginDataEntry>> ListAsync(
        PluginDataCollectionKey collection, CancellationToken ct = default);

    // All workspace keys that hold documents of a collection, excluding global data.
    Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(
        string pluginId, string collection, CancellationToken ct = default);
}
```

Payloads are **raw JSON strings** — you own serialization. `GetAsync` returns `null` when the
document doesn't exist; `RemoveAsync` returns `false` when there was nothing to remove;
`ListAsync` returns `PluginDataEntry(EntryKey, JsonDocument, UpdatedAtUtc)` ordered by entry
key.

## Reading and writing

From `DataStoreSipAccountStore`, listing a workspace's documents in one collection:

```csharp
private const string Collection = "sip-accounts";

var entries = await dataStore.ListAsync(
    new PluginDataCollectionKey(CommunicationPlugin.Id, workspaceKey, Collection),
    cancellationToken);

foreach (var entry in entries)
{
    var account = JsonSerializer.Deserialize<SipAccountEntry>(entry.JsonDocument, JsonOptions);
    // …
}
```

Writing one document (serialize your object, then `SetAsync`):

```csharp
var key = new PluginDataKey(CommunicationPlugin.Id, workspaceKey, Collection, sipAccountId);
var json = JsonSerializer.Serialize(persistable, JsonOptions);
await dataStore.SetAsync(key, json, cancellationToken);
```

Removing:

```csharp
var removed = await dataStore.RemoveAsync(
    new PluginDataKey(CommunicationPlugin.Id, workspaceKey, Collection, sipAccountId),
    cancellationToken);
```

**Expected behavior:** `SetAsync` creates or replaces the document at that exact key.
`GetAsync` on the same key returns your JSON verbatim; a missing key returns `null`.

## Protecting sensitive values

A JSON document sitting in the data store is not encrypted by default. **Never put a raw
password, token, or API key straight into a document.** Encrypt it first with
`IPluginDataProtector` (`src/Core/Application/Secrets/Contracts/`):

```csharp
public interface IPluginDataProtector
{
    string Protect(string pluginId, string plaintext);

    // Returns false for a value not protected for this plugin (e.g. legacy plaintext).
    bool TryUnprotect(string pluginId, string protectedValue, out string plaintext);
}
```

Payloads are **isolated per plugin**: a value protected for `communication` cannot be
unprotected for another plugin. From `DataStoreSipAccountStore`, the secret is encrypted on
the way in and decrypted on the way out:

```csharp
// Write: encrypt the secret before serializing the document.
var persistable = entry with { Secret = dataProtector.Protect(CommunicationPlugin.Id, entry.Secret) };
var json = JsonSerializer.Serialize(persistable, JsonOptions);
await dataStore.SetAsync(BuildKey(workspaceKey, entry.SipAccountId), json, cancellationToken);

// Read: decrypt, tolerating legacy plaintext.
private string UnprotectSecret(string storedSecret) =>
    dataProtector.TryUnprotect(CommunicationPlugin.Id, storedSecret, out var plaintext)
        ? plaintext
        : storedSecret; // legacy plaintext stays readable; re-encrypted on next write
```

::: warning `TryUnprotect` returning false is not an error
`TryUnprotect` returns `false` for a value that isn't a valid protected payload for your
plugin — for example a legacy plaintext value written before you added encryption. The
Communication store keeps that value readable and re-encrypts it on the next write. Decide
your own migration policy; don't treat `false` as data loss.
:::

::: info `sensitiveFields` in the manifest is a different mechanism
The `sensitiveFields` list in [`registry.json`](/guides/fundamentals/registry-manifest) drives
**webhook data-minimization** — it marks person-related payload fields the platform redacts
from outbound webhooks. Encrypting a stored value is `IPluginDataProtector`'s job. They both
protect sensitive data, but at different points; use both where each applies. See
[Compliance metadata](/guides/fundamentals/compliance-metadata).
:::

## Data store or EF entities?

- **Data store** — a small, bounded set of JSON documents keyed by id; no querying beyond
  "get by key" and "list a collection"; you want zero migration overhead.
- **[EF entities](./entities-and-schemas)** — you need columns, indexes, relations, ordering,
  or filtering in the database; the dataset grows large. The Communication plugin actually
  migrated its SIP accounts from the data store to EF for exactly these reasons (it keeps the
  data-store store as a fallback).

## Cleaning up on purge

Documents you write with a `WorkspaceKey` are your plugin's workspace-scoped data. The host
cannot delete them for you — export an `IWorkspaceDataPurgeContributor` and use
`ListWorkspaceKeysAsync` + `RemoveAsync` (or the EF equivalent) to erase them. See
[Retention & GDPR](./retention-and-gdpr).

## Next steps

- Encrypt operator-provided credentials instead of storing them: **[Secrets](./secrets)**
- Erase these documents on workspace purge: **[Retention & GDPR](./retention-and-gdpr)**
- Move to typed rows when you outgrow key/value: **[Entities & schemas](./entities-and-schemas)**
- Manifest `sensitiveFields`: **[Registry manifest](/guides/fundamentals/registry-manifest)** · **[Compliance metadata](/guides/fundamentals/compliance-metadata)**
- Contract signatures: **[.NET API](/api/)**
