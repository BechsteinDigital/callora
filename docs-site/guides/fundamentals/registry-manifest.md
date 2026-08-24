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
| `contractVersion` | **Yes** | Host↔plugin contract generation. Use `"v2"` — validated against `PluginContractVersionPolicy`, where `v2` is supported, `v1` deprecated (installs with a warning) and `v0` removed (rejected). |
| `schemaVersion` | **Yes** | Version of the `registry.json` schema itself (e.g. `"1.0"`). |
| `name` | **Yes** | Human-readable display name for tooling and the marketplace. |
| `pluginId` | **Yes** | Stable machine identifier. Must equal the entry class's `PluginId`. Also derives your DB schema and asset root — see below. |
| `version` | **Yes** | The plugin's own semantic version (e.g. `"0.2.0"`). |
| `assemblyFileName` | **Yes** | File name of the compiled entry assembly (e.g. `"Callora.Plugin.Communication.dll"`). |
| `entryTypeName` | **Yes** | Fully-qualified type name of the `IHostManagedPlugin` implementation. |
| `tier` | Optional | Deployment tier: `"system"` (foundation) or `"application"` (default). |
| `capabilities` | Optional | Capability strings this plugin **provides** (e.g. `"communication.voice"`). Trimmed and de-duplicated. |
| `requiresCapabilities` | Optional | Capability strings this plugin **requires** another active plugin to provide. |
| `dependencies` | Optional | Map of package name → version range (e.g. `"Callora.Core": ">=0.9.0"`). Enforced at install time — see [plugin dependencies](./plugin-dependencies). |
| `extensions` | Optional | Declared extension-point participations — an array of `{ extensionPointId, surface }`. |
| `permissions` | Optional | Permission keys this plugin's routes require — an array of `{ key, description }`. Each key must sit inside the plugin's own namespace and end in a known action; anything else makes the manifest invalid. |
| `databaseSchema` | Optional | Explicit EF schema name for cleanup on uninstall. Read separately (see [Fields read outside the core parser](#fields-read-outside-the-core-parser)). |
| `sensitiveFields` | Optional | Person-related payload field names for webhook data-minimization. Read separately. |

## Declaring the permissions your routes require

`CalloraRouteAttribute.Permission` lets a route **demand** a permission key. Until you declare
it here, nothing can **supply** one — the key exists only in your source, and an operator has
no way to grant it. A plugin in that state installs, activates, and answers `403` forever.

```json
"permissions": [
  { "key": "communication.trunk.update", "description": "Reconfigure a SIP trunk" },
  { "key": "communication.call.execute",  "description": "Place an outbound call" }
]
```

The manifest carries it — not `context.Export(...)` — although [ADR-009](https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-009-code-first-extension-wiring.md)
otherwise puts wiring in code. An operator has to see what a plugin will ask for **before**
installing it, and a declaration that only exists once the plugin runs is too late for that
decision.

### Two rules, both enforced at read time

**The key must sit inside your own namespace** — it begins with your `pluginId` and a dot.
Declaration is self-service, so without this a plugin could declare `user.delete` and have an
operator grant it in good faith, believing it to be the plugin's own. The separator is part of
the check: a plugin called `communications` cannot declare `communication.read`.

**The key must end in a known action** — `create`, `read`, `update`, `delete` or `execute`.
Keys are granted through role-function-action configuration; one that cannot be expressed
there would move the dead end rather than remove it.

A key breaking either rule makes the **whole manifest invalid**
(`PLUGIN_PERMISSION_NOT_DECLARABLE`) rather than being skipped. Skipping would put the plugin
back in the state this exists to fix: installed, serving `403`, with the reason two layers
down. Repeating the same key is collapsed, not refused — untidy, not dangerous.

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
`custom/static-plugins/Communication/registry.json`, verbatim — including its
`contractVersion` of `v1`, which is the *deprecated* tier. It installs, with a warning. A new
plugin should declare `v2`; this one is quoted as it stands, not as a template.

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

Contrast the **consumer** side. A dialer plugin *requires* the capability Communication
provides, and provides none of its own:

```json
{
  "contractVersion": "v2",
  "schemaVersion": "1.0",
  "name": "Acme Dialer",
  "pluginId": "acme-dialer",
  "version": "0.1.0",
  "assemblyFileName": "Acme.Dialer.dll",
  "entryTypeName": "Acme.Dialer.DialerPlugin",
  "capabilities": [],
  "requiresCapabilities": [
    "communication.voice"
  ],
  "dependencies": {
    "Callora.Core": ">=0.9.0",
    "Callora.Plugin.Communication.Abstractions": ">=0.9.0"
  }
}
```

It provides no capabilities of its own, requires `communication.voice`, and declares no
tier — so it defaults to `application`. This provider/consumer pairing is exactly how
`requiresCapabilities` and `capabilities` work together: the host can gate the dialer's
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
