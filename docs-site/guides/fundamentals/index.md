# Plugin Fundamentals

Everything a Callora plugin *is* comes down to a handful of building blocks: a **runtime
entry class**, a **`registry.json` manifest**, and a **curated plugin context** the host
hands you at startup. Master those three and the rest — HTTP APIs, events, jobs, custom
entities — are just services you export from one method.

These fundamentals pages take you from "what is an entry class" to a plugin that resolves
host services, exports its own contracts, and declares its metadata correctly. They assume
you've already built and run a plugin once; if you haven't, start with
[Build your first Callora plugin](/guides/getting-started/your-first-plugin) and come back
here to understand *why* each piece works the way it does.

## What you'll learn

- The three pillars every plugin rests on: the entry class, the manifest, and the context
- Where extension wiring actually happens (spoiler: in code, not in the manifest)
- How Callora's plugin model differs from a plain ASP.NET DI container — and why
- The recommended reading order to go from fundamentals to shipping a real plugin

## The three pillars

A Callora plugin is a normal .NET class library with three things the host cares about:

| Pillar | Artifact | Role |
| --- | --- | --- |
| **Entry class** | A class implementing `IHostManagedPlugin` | The runtime lifecycle hook — the host calls `StartAsync`/`StopAsync` on it |
| **Manifest** | `registry.json` next to the assembly | Governance metadata: identity, version, tier, capabilities, dependencies |
| **Context** | `IHostPluginContext` (passed to `StartAsync`) | The curated surface for resolving host services and exporting your own |

The key idea that surprises people coming from other plugin systems: **extension wiring is
code-first**. Your `registry.json` does *not* wire up your services — it declares who you
are and what you require. The actual "here is my controller / my event listener / my
service" happens in code, through `context.Export(...)` inside `StartAsync`. The manifest
is metadata the host reads for identity, trust, capability gating, and cleanup; the code is
where behavior is attached.

## The learning path

Read these in order. Each builds on the last.

1. **[The plugin entry class](./plugin-entry)** — `IHostManagedPlugin`: `PluginId`,
   `DisplayName`, `StartAsync`, `StopAsync`. What belongs in each method, and how the
   activate/deactivate lifecycle drives them.
2. **[The registry manifest](./registry-manifest)** — every field of `registry.json`,
   which are required, and how `pluginId` doubles as your database schema and asset-root
   segment.
3. **[The plugin context & dependency injection](./dependency-injection)** —
   `IHostPluginContext`: resolving host services from the *curated* `Services` provider and
   publishing your own implementations with `Export`.
4. **[Exporting extensions](/guides/fundamentals/exporting-extensions)** — the export
   mechanism in depth: controllers, event listeners, flow actions, and how the host
   resolves them back through `ICalloraPluginCatalog`.
   > **Status:** page planned — see [Backend Extensions](/guides/backend-extensions) for the
   > current coverage of export-based extension points.
5. **[Plugin configuration](/guides/fundamentals/plugin-configuration)** — reading typed
   settings and secrets via `IPluginConfigReader` and `ISecretStore`.
   > **Status:** page planned.
6. **[Plugin dependencies](/guides/fundamentals/plugin-dependencies)** — the `dependencies`
   and `requiresCapabilities` fields, and cross-plugin contract packages.
   > **Status:** page planned.
7. **[Compliance metadata](/guides/fundamentals/compliance-metadata)** —
   `sensitiveFields`, `databaseSchema`, and how the host uses them for data minimization and
   cleanup.
   > **Status:** page planned.
8. **Best practices** — putting it together: small classes, deterministic startup, clean
   teardown.
   > **Status:** page planned.

## How this differs from a plain .NET host

If you've written an ASP.NET Core app, you're used to `IServiceProvider` giving you *any*
registered service and `Program.cs` wiring everything at boot. Callora deliberately narrows
both:

- The `Services` provider you get is **curated** — it exposes published contracts and
  cross-plugin exports, not the host's full container. This is a trust boundary, not an
  oversight. See [dependency injection](./dependency-injection) for the exact rules.
- There is no central composition root you edit. A plugin is **discovered, installed, and
  activated at runtime** — hot, without recompiling or restarting the host — and it wires
  *itself* in `StartAsync`.

::: info
Callora's plugin model is closer to Shopware's or Umbraco's than to a typical .NET
microservice: plugins are units of governance and lifecycle, loaded into collectible
assembly load contexts and unloadable at runtime.
:::

## Next steps

- Start the path: **[The plugin entry class](./plugin-entry)**
- Haven't built a plugin yet? **[Build your first Callora plugin](/guides/getting-started/your-first-plugin)**
- The bigger picture: **[Architecture concepts](/concepts/architecture)**
- API reference: **[.NET API reference](/api/)**
