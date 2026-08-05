# The registry manifest

Every Callora plugin ships a **`registry.json`** at its root, next to (or above) its
compiled assembly. It's the plugin's identity card: who the plugin is, what version it is,
which tier it deploys into, what capabilities it offers or requires, and which packages it
depends on. The host reads it during discovery, install, and activation.

The one thing it is **not**: a wiring file. Your extensions — controllers, event listeners,
services — are attached in code via `context.Export(...)`, not declared here. Think of the
manifest as *governance metadata*, and the entry class as *behavior*.

## What you'll learn

- Every field of `registry.json`: which are required, which are optional, and their purpose
- How the host parses the manifest (`JsonPluginPackageRegistryReader` → `PluginRegistryJsonDto`)
- Why `pluginId` is special — it's also your database schema and asset-root segment
- A complete, real example from the Communication plugin

::: tip Prerequisites
Read [the plugin entry class](./plugin-entry) first — several manifest fields
(`pluginId`, `entryTypeName`) point directly at your entry class.
:::

## Where the manifest lives and how it's found

The host resolves the manifest by walking up from the plugin's assembly directory until it
finds a `registry.json` (`JsonPluginPackageRegistryReader.ResolveRegistryPath`). In practice
that means `registry.json` sits at the **plugin root**, and the compiled assembly lands
under `bin/…/net10.0/` beneath it. You keep one manifest per plugin, at the top.

## The fields

The core manifest is parsed into `PluginRegistryJsonDto`
(`src/Core/Infrastructure/Plugins/PluginRegistryJsonDto.cs`) and validated by
`JsonPluginPackageRegistryReader`. Field names are matched **case-insensitively**.

| Field | Required | Purpose |
| --- | --- | --- |
| `contractVersion` | **Yes** | Host↔plugin contract generation (e.g. `"v1"`). Validated against `PluginContractVersionPolicy`; an unsupported or removed version rejects the plugin, a deprecated one warns. |
| `schemaVersion` | **Yes** | Version of the `registry.json` schema itself (e.g. `"1.0"`). |
| `name` | **Yes** | Human-readable display name for tooling and the marketplace. |
| `pluginId` | **Yes** | Stable machine identifier. Must equal the entry class's `PluginId`. Also derives your DB schema and asset root — see below. |
| `version` | **Yes** | The plugin's own semantic version (e.g. `"0.2.0"`). |
| `assemblyFileName` | **Yes** | File name of the compiled entry assembly (e.g. `"Callora.Plugin.Communication.dll"`). |
| `entryTypeName` | **Yes** | Fully-qualified type name of the `IHostManagedPlugin` implementation. |
| `tier` | Optional | Deployment tier: `"system"` (foundation) or `"application"` (default). |
| `capabilities` | Optional | Capability strings this plugin **provides** (e.g. `"communication.voice"`). Trimmed and de-duplicated. |
| `requiresCapabilities` | Optional | Capability strings this plugin **requires** another active plugin to provide. |
| `dependencies` | Optional | Map of package name → version range (e.g. `"Callora.Host.PluginContracts": ">=0.1.0"`). |
| `extensions` | Optional | Declared extension-point participations — an array of `{ extensionPointId, surface }`. |
| `databaseSchema` | Optional | Explicit EF schema name for cleanup on uninstall. Read separately (see [Fields read outside the core parser](#fields-read-outside-the-core-parser)). |
| `sensitiveFields` | Optional | Person-related payload field names for webhook data-minimization. Read separately. |

::: warning The eight required fields
`JsonPluginPackageRegistryReader` rejects the manifest with a clear error if any of
`contractVersion`, `schemaVersion`, `name`, `pluginId`, `version`, `assemblyFileName`, or
`entryTypeName` is missing or blank — and if `contractVersion` isn't a supported value. A
malformed or removed `contractVersion` fails the plugin outright; a deprecated one activates
with a warning.
:::

### Fields read outside the core parser

`databaseSchema` and `sensitiveFields` are **not** part of `PluginRegistryJsonDto`. They're
read on demand by dedicated services straight from the JSON, so the core parser stays lean
and these concerns live with the subsystems that own them:

- **`databaseSchema`** — read by `PluginManifestSchemaReader.TryReadDatabaseSchema`
  (`src/Core/Infrastructure/Persistence/`). It lets a plugin declare its EF schema
  explicitly so the host drops exactly that schema on uninstall instead of guessing
  `plugin_<id>`. The value is sanitized as a safe DDL identifier.
- **`sensitiveFields`** — read by `RegistrySensitiveFieldSyncService.ParseSensitiveFields`
  (`src/Core/Infrastructure/Webhooks/`). The listed field names are synced into the
  `SensitivePayloadFieldRegistry` so outbound webhook payloads mask them — the core never
  hardcodes a domain field.

::: info Compliance metadata
`sensitiveFields` and `databaseSchema` are the manifest's compliance/data-governance surface
today. A dedicated [Compliance metadata](/guides/fundamentals/compliance-metadata) page will
cover the full data-handling story.

> **Status:** the Communication manifest below declares `sensitiveFields` and
> `databaseSchema`; broader compliance metadata beyond these two fields is not yet part of
> the manifest.
:::

## `pluginId` is more than an id

`pluginId` is the one field that leaks into the rest of the platform. Beyond identity, it
determines:

- **Your database schema.** `PluginSchemaName`
  (`src/Core/Infrastructure/Persistence/PluginSchemaName.cs`) turns your `pluginId` into
  `plugin_<id>` — e.g. `communication` → `plugin_communication`. That's the schema your own
  EF context lives in, isolated from every other plugin. (You can override the exact name
  with the optional `databaseSchema` field.)
- **Your asset-root segment.** The UI asset publisher (`PluginUiAssetPublisher`) roots your
  static assets under a path segment derived from `pluginId`, keyed by the same id read back
  from `registry.json`.
- **Your data partition.** The curated `IPluginDataStore` you receive is *plugin-bound* by
  `pluginId`, so one plugin can never address another's key/value data.

Pick a `pluginId` once and never change it — renaming it orphans your schema, assets, and
stored data.

## A complete example

Here is the Communication plugin's manifest,
`custom/static-plugins/Communication/registry.json`, verbatim:

```json
{
  "contractVersion": "v1",
  "schemaVersion": "1.0",
  "name": "Communication",
  "pluginId": "communication",
  "version": "0.1.0",
  "assemblyFileName": "Callora.Plugin.Communication.dll",
  "entryTypeName": "Callora.Plugin.Communication.CommunicationPlugin",
  "capabilities": [
    "communication.foundation"
  ],
  "conditionalCapabilities": [
    "communication.voice",
    "communication.video",
    "communication.webrtc"
  ],
  "sensitiveFields": [
    "remoteParty"
  ],
  "dependencies": {
    "Callora.Core": ">=0.1.0-local",
    "Callora.Plugin.Communication.Abstractions": ">=0.1.0-local"
  }
}
```

Reading it top to bottom: this is a plugin named *Communication*, id `communication`,
version `0.1.0`. Its entry class is `CommunicationPlugin`, in
`Callora.Plugin.Communication.dll`. It **provides** `communication.foundation`
unconditionally and `communication.voice` / `communication.video` /
`communication.webrtc` only while the corresponding runtime dependency is healthy.
It marks `remoteParty` — the telephone number its call events carry — as sensitive,
so webhook data-minimization masks it by default, and depends on the core plus its
own abstractions package.

::: warning Declare the field names you actually emit
The masking registry matches **property names in the serialized payload**, case-insensitively.
`CallBusinessEvent.ToEventData()` emits `remoteParty`, so that is the name that must appear
here — a plausible-looking `phoneNumber` would mask nothing. When you change an event's
schema, update `sensitiveFields` in the same commit.
:::

Contrast the **consumer** side — the Dialer plugin's
`custom/plugins/Dialer/registry.json` — which *requires* the capability Communication
provides:

```json
{
  "contractVersion": "v1",
  "schemaVersion": "1.0",
  "name": "Callora Dialer Plugin",
  "pluginId": "dialer",
  "version": "0.1.0",
  "assemblyFileName": "Callora.Plugins.Dialer.dll",
  "entryTypeName": "Callora.Plugins.Dialer.DialerPlugin",
  "capabilities": [],
  "requiresCapabilities": [
    "communication.voice"
  ],
  "dependencies": {
    "Callora.Host.PluginContracts": ">=0.1.0",
    "Callora.Plugin.Communication.Abstractions": ">=0.1.0"
  }
}
```

Dialer provides no capabilities of its own, requires `communication.voice`, and declares no
tier — so it defaults to `application`. This provider/consumer pairing is exactly how
`requiresCapabilities` and `capabilities` work together: the host can gate Dialer's
activation on Communication being active. See
[plugin dependencies](/guides/fundamentals/plugin-dependencies) for the full contract.

## The manifest is metadata, not wiring

Worth repeating, because it's the most common misconception: **nothing in `registry.json`
attaches an extension.** No route is declared here; no event listener is registered here. The
manifest tells the host *who you are and what you require*; your `StartAsync` tells it *what
you do*, via `context.Export(...)`. Extension wiring is code-first by design — the manifest
is the governance layer around it.

## Next steps

- Wire the behavior the manifest describes: **[Dependency injection & exports](./dependency-injection)**
- The entry class the manifest points at: **[The plugin entry class](./plugin-entry)**
- Capabilities and dependencies in full: **[Plugin dependencies](/guides/fundamentals/plugin-dependencies)**
- Field-level reference: **[Extension manifests reference](/reference/extension-manifests)**
