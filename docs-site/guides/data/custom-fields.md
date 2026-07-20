# Custom Fields

Sometimes you don't need a table of your own — you need to hang one extra value off a record
the *host* owns. A CRM plugin wants a "loyalty tier" on a workspace; an onboarding plugin
wants a "signup source" on a user. Custom fields are how a plugin attaches typed extra fields
to core entities and reads or writes their values, without altering the host schema.

This page shows the `CustomFieldDefinition` / `CustomFieldValue` model, how a plugin reads and
writes values through `ICustomFieldAccessor`, and the operator-facing `/api/custom-fields`
endpoints.

## What you'll learn

- The difference between a **definition** (the field) and a **value** (what's stored on one
  record)
- How to read and write values from plugin code via `ICustomFieldAccessor`
- The `/api/custom-fields` operator endpoints and their permissions and scoping
- Why workspace-bound values get cleaned up automatically on GDPR purge

::: tip Prerequisites
- A running plugin with `context.Services` access — see [Plugin entry](/guides/fundamentals/plugin-entry).
- You know the target entity name (for example `workspace`) and the id of the concrete
  instance (for a workspace, that's the **workspace key**).
:::

## Definitions vs. values

Two domain types, both under `src/Core/Domain/CustomFields/`:

**`CustomFieldDefinition`** — declares a field: which plugin owns it, which entity it attaches
to, its key, label, and type.

```csharp
public sealed class CustomFieldDefinition
{
    public Guid Id { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty; // e.g. "workspace", "user"
    public string FieldKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FieldType { get; set; } = "text";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

**`CustomFieldValue`** — one stored value on one concrete entity instance:

```csharp
public sealed class CustomFieldValue
{
    public Guid Id { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;  // e.g. the workspace key
    public string FieldKey { get; set; } = string.Empty;
    public string ValueJson { get; set; } = string.Empty; // values are JSON-encoded strings
    public string? WorkspaceKey { get; set; }             // set for workspace-bound values
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

::: info Values are JSON-encoded strings
`ICustomFieldAccessor` deals in `string` values that are JSON-encoded — a text field is
`"\"gold\""`, a number is `"42"`. Encode and decode with `System.Text.Json` on your side.
:::

## Reading and writing values from a plugin

Resolve `ICustomFieldAccessor` (`src/Core/Application/CustomFields/Contracts/`) from
`context.Services`. It's a small two-method contract:

```csharp
public interface ICustomFieldAccessor
{
    Task<IReadOnlyDictionary<string, string>> GetValuesAsync(
        string entityName, string entityId, CancellationToken ct = default);

    // A null value clears the field.
    Task SetValuesAsync(
        string entityName, string entityId,
        IReadOnlyDictionary<string, string?> valuesByKey, CancellationToken ct = default);
}
```

Reading the values on a workspace:

```csharp
var accessor = (ICustomFieldAccessor)
    context.Services.GetService(typeof(ICustomFieldAccessor))!;

IReadOnlyDictionary<string, string> values =
    await accessor.GetValuesAsync("workspace", workspaceKey, ct);

if (values.TryGetValue("loyalty_tier", out var raw))
{
    var tier = JsonSerializer.Deserialize<string>(raw); // JSON-encoded
}
```

Writing (or clearing) values:

```csharp
await accessor.SetValuesAsync(
    "workspace",
    workspaceKey,
    new Dictionary<string, string?>
    {
        ["loyalty_tier"]  = JsonSerializer.Serialize("gold"), // set
        ["legacy_flag"]   = null,                             // clear
    },
    ct);
```

**Expected behavior:** `SetValuesAsync` upserts each key; a `null` value deletes that entry.
`GetValuesAsync` returns only the keys that currently have a value, keyed by field key.

::: info The accessor is safe to hold as a singleton
`ScopedCustomFieldAccessor` (`src/Core/Application/CustomFields/`) is a singleton facade that
opens a fresh DI scope per call and delegates to the scoped `ICustomFieldStore`. You can
resolve it once and reuse it.
:::

## The operator API — `/api/custom-fields`

Operators (and admin UIs) manage definitions and values over HTTP. The routes live in
`CustomFieldEndpoints` (`src/Administration/Api/`), all behind authorization:

| Method & route | Permission | Purpose |
| --- | --- | --- |
| `GET /api/custom-fields/definitions?entityName=…` | `customfield.read` | List field definitions (optionally filtered by entity) |
| `GET /api/custom-fields/{entityName}/{entityId}` | `customfield.read` | Read the values stored on one entity instance |
| `PUT /api/custom-fields/{entityName}/{entityId}` | `customfield.update` | Upsert values on one entity instance |

The `PUT` body is an `UpsertCustomFieldValuesApiRequest` — a map of field key to a raw JSON
value (`null` clears):

```json
PUT /api/custom-fields/workspace/acme-workspace
{
  "valuesByKey": {
    "loyalty_tier": "gold",
    "legacy_flag": null
  }
}
```

Returns `204 No Content` on success.

::: warning Workspace values are workspace-scope enforced
For the `workspace` entity, `entityId` **is** the workspace key, and the endpoint checks the
caller actually has access to that workspace
(`WorkspaceScopeEvaluator.HasWorkspaceAccess`). Other entity types currently carry no
per-entity ownership, so they stay **operator-only**
(`WorkspaceScopeEvaluator.IsOperator`). Don't assume a non-workspace entity is reachable by a
workspace-scoped user.
:::

## Declaring definitions

> **Status:** the `/api/custom-fields` surface above exposes reading definitions
> (`GET /definitions`) and reading/writing **values**. Definitions themselves are managed
> through `ICustomFieldStore` (`src/Core/Application/CustomFields/`), whose
> `ReplaceDefinitionsForPluginAsync(pluginId, version, definitions, ct)` and
> `ClearDefinitionsForPluginAsync(pluginId, ct)` let a plugin register its field set
> (idempotently, per version) and clear it. There is no public *write*-definition HTTP
> endpoint in `CustomFieldEndpoints` today; register your definitions from plugin code via
> `ICustomFieldStore` when your plugin activates.

## Cleanup on GDPR purge

`CustomFieldValue.WorkspaceKey` is set for workspace-bound values specifically so the host
can **cascade-delete them when a workspace is purged** (PLAT-245). You don't write cleanup
code for custom field values on host entities — the platform handles them. Data your plugin
stores in *its own* schema is a different story: see [Retention & GDPR](./retention-and-gdpr).

## Next steps

- Erase your *own* plugin data on purge: **[Retention & GDPR](./retention-and-gdpr)**
- Model richer plugin-owned data instead: **[Entities & schemas](./entities-and-schemas)**
- The full endpoint list and auth model: **[REST API](/reference/rest-api)**
- Permission keys and RBAC: **[Backend extensions](/guides/backend-extensions)**
- Contract signatures: **[.NET API](/api/)**
