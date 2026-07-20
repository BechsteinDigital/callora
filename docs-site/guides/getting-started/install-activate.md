# Install and activate plugins

This guide explains the Callora plugin lifecycle from an operator's point of view: the
states a plugin moves through, and the operator API you call to drive those transitions.
Everything here happens **hot** — installing, activating, and deactivating a plugin never
requires a host restart.

## What you'll learn

- The plugin state model and which transition each API call performs
- The operator API routes for installing, activating, deactivating, and uninstalling
- Which RBAC permission each route requires
- How hot-loading works (collectible load contexts, live activation)
- The dev-only `AllowUnsignedPlugins` escape hatch

::: tip Prerequisites
- A running Callora host with an operator account.
- Operator credentials with the plugin permissions below. In development the bootstrap API
  key authenticates as a platform operator (see
  [Build your first plugin](/guides/getting-started/your-first-plugin)).
- To install a *local* plugin, its project must live under the host's plugin directory
  (e.g. `custom/plugins/<name>`). See [Plugin project layout](/guides/getting-started/project-layout).
:::

## The state model

A plugin installation is tracked as a single state in the database — the database is the
source of truth for what is installed and active, not the file system. The states are
defined by `PluginInstallationState`:

| State | Meaning |
| --- | --- |
| `Installed` | Registered and known to the host, but not running. |
| `Active` | Loaded and running. Its exports (routes, services, UI) are live. |
| `Inactive` | Previously active, now deactivated. Still installed. |
| `Uninstalled` | Terminal. Removed from the host. |

The transitions map directly onto API calls:

```
install ──▶ Installed ──activate──▶ Active
                 ▲                     │
                 │                deactivate
                 │                     ▼
              (activate)◀────────  Inactive
                 │
                 └──uninstall──▶ Uninstalled  (terminal)
```

- **Install** brings a plugin to `Installed`.
- **Activate** loads and runs it (`Active`). It works from `Installed` or `Inactive`.
- **Deactivate** stops it (`Inactive`) without uninstalling.
- **Uninstall** removes it (`Uninstalled`).

::: info The database is the source of truth
On startup the host rehydrates active plugins from the database and re-verifies their
signatures. Dropping a DLL into a folder does **not** activate a plugin — a state
transition is always recorded through the API.
:::

## The operator API

All routes live under `/api/plugins` and require authentication. Each route below also
requires a specific RBAC permission (shown per route). The four plugin permission keys are:

| Permission key | Value | Used by |
| --- | --- | --- |
| `PluginRead` | `plugin.read` | list, installed, signature-report, audit |
| `PluginCreate` | `plugin.create` | install (all variants) |
| `PluginExecute` | `plugin.execute` | activate, deactivate |
| `PluginDelete` | `plugin.delete` | uninstall |

### Install a plugin

There are three install routes, one per source. All require `plugin.create`.

**From an assembly path** — `POST /api/plugins/install`:

```json
{
  "assemblyPath": "/abs/path/to/Callora.Plugins.MyPlugin.dll",
  "entryTypeName": "Callora.Plugins.MyPlugin.Application.MyPluginPlugin",
  "requestedBy": "operator@example.com"
}
```

**From a local project (the dev path)** — `POST /api/plugins/install/local`. This resolves
a plugin by its `pluginId` under the host's plugin directory and, when asked, builds it
before installing:

```json
{
  "pluginId": "my-plugin",
  "buildIfNeeded": true,
  "forceBuild": false,
  "requestedBy": "operator@example.com"
}
```

`buildIfNeeded` builds the project if no up-to-date output exists; `forceBuild` rebuilds
unconditionally. The resolver locates the DLL and entry type for you, then hands off to the
same install as above.

**From a NuGet package** — `POST /api/plugins/install/nuget`:

```json
{
  "packageId": "Acme.Callora.MyPlugin",
  "packageVersion": "1.0.0",
  "assemblyFileName": "Acme.Callora.MyPlugin.dll",
  "entryTypeName": "Acme.Callora.MyPlugin.MyPluginPlugin",
  "requestedBy": "operator@example.com"
}
```

Expected result on success (all install routes): HTTP `200` with a body like:

```json
{ "isSuccess": true, "pluginId": "my-plugin", "message": "...", "errorCode": null }
```

On a validation failure you get HTTP `400` with `isSuccess: false` and an `errorCode`.

### Activate and deactivate

`POST /api/plugins/{id}/activate` and `POST /api/plugins/{id}/deactivate` — both require
`plugin.execute`:

```json
{ "requestedBy": "operator@example.com", "workspaceKey": null }
```

Activation loads the plugin and brings up its exports live. Deactivation stops it and frees
its load context. Neither restarts the host.

### Uninstall

`DELETE /api/plugins/{id}?requestedBy=operator@example.com` — requires `plugin.delete`.
This moves the plugin to the terminal `Uninstalled` state.

### Read and inspect

These read-only routes require `plugin.read`:

| Route | Returns |
| --- | --- |
| `GET /api/plugins` | The in-memory list of known plugins. |
| `GET /api/plugins/installed` | Installations from the store. For callers with `plugin.create`, the plugin directories are reconciled against the registry first, so the list reflects the file system. |
| `GET /api/plugins/signature-report` | Each installed plugin's **current** signature standing (signed-trusted / unsigned / untrusted / revoked / hash-mismatch), re-verified on request. |
| `GET /api/plugins/audit` | Recent lifecycle audit entries (default 200; pass `?take=`). |

## Hot-loading, in detail

Callora loads each plugin into its own **collectible** `AssemblyLoadContext`
(`isCollectible: true`). That has two consequences an operator should know:

- **Activate/deactivate is live.** No host restart, no recompile of the platform. A plugin
  you activate starts serving immediately; one you deactivate has its load context unloaded.
- **Host and plugin share contract types.** `Callora.*` contract assemblies resolve from the
  host's default context, so a type like `IHostManagedPlugin` is the *same* type on both
  sides. Plugin-private dependencies stay in the plugin's context.

::: warning Signatures are enforced at install and at load
By default a plugin must carry a trusted content signature. In **development** you can set
`BackendHost:AllowUnsignedPlugins` to `true` to install and run locally built, unsigned
plugins. Do **not** enable this in production — it disables the trust gate. See
[The `callora` CLI](/guides/getting-started/plugin-cli#plugin-sign) for signing.
:::

## Next steps

- [Build your first plugin](/guides/getting-started/your-first-plugin) — the end-to-end
  install/activate walkthrough with a real `curl`.
- [Plugin project layout](/guides/getting-started/project-layout) — set up a project the
  local install path can resolve and build.
- [REST API reference](/reference/rest-api) — the full `/api/plugins` surface.
- [Platform architecture](/concepts/architecture) — how load contexts and rehydration fit
  together.
