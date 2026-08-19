# Plugin project layout

A Callora plugin is a normal .NET class-library project plus a `registry.json` manifest.
This guide describes the recommended structure — how to organize code by DDD layers, where
each artifact belongs, and the contract rules your `.csproj` must follow. It is the layout
`callora plugin new` scaffolds, and the one the first-party plugins use.

## What you'll learn

- The recommended folder structure and what each folder holds
- Where `registry.json`, the `.csproj`, and UI resources live
- How source UI (`Resources/app`) differs from the built bundle (`Resources/public`)
- The one reference that binds you to the platform, and why you should not hand-roll it

::: tip Prerequisites

- The [`callora` CLI](/guides/getting-started/plugin-cli), which scaffolds this layout for
  you with `plugin new`.
- Familiarity with the [plugin entry contract](/guides/fundamentals/plugin-entry).
:::

## Recommended structure

Callora's engineering rules favour small, single-responsibility classes — **one public type
per file** — organized by DDD layer. A fuller plugin looks like this:

```text
my-plugin/
├─ Acme.MyPlugin.csproj               # project file
├─ registry.json                     # manifest (required)
└─ src/
   ├─ MyPluginPlugin.cs              # IHostManagedPlugin entry type
   ├─ Domain/                        # entities, value objects, domain contracts
   ├─ Application/                   # use cases, route handlers, services, stores
   │  ├─ Admin/                      # admin API route handlers, permission keys
   │  └─ …
   ├─ Infrastructure/                # persistence, external integrations, EF migrations
   └─ Resources/
      ├─ app/<surface>/src/          # UI source (author-only, not shipped)
      └─ public/<surface>/           # built UI bundle (shipped)
```

### What each layer holds

- **Domain** — the plugin's core model: entities, value objects, enums, and the interfaces
  that describe its own domain. No framework or persistence concerns.
- **Application** — use cases and orchestration: route handlers, coordinators, service
  interfaces and their in-memory or data-store backed implementations. The `Application/Admin`
  folder is a common home for admin-API route handlers and the plugin's permission keys.
- **Infrastructure** — the outside world: EF Core `DbContext`, migrations, and adapters to
  external SDKs. The `Communication` plugin, for example, keeps its `VoipDbContext`,
  `Migrations/`, and SDK-engine adapters here.

::: info One type per file
Per Callora's code-structure rules, avoid `partial` classes and monolithic "god" files.
Keep classes small and give each public type its own file — the shipped plugins follow this
strictly (each store, handler, and event type is its own file).
:::

## `registry.json`

Every plugin ships a `registry.json` next to its assembly. It declares the contract version,
the entry type, and the plugin's capabilities:

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
  "requiresCapabilities": ["communication.voice"],
  "dependencies": {
    "Callora.Core": ">=0.9.0",
    "Callora.Plugin.Communication.Abstractions": ">=0.9.0"
  }
}
```

Key fields: `contractVersion` (`v2` is the supported version; `v1` still installs but warns, `v0` is refused), `schemaVersion`, `name`, `pluginId`,
`version`, `assemblyFileName` (must match the built DLL), and `entryTypeName` (the full
.NET type name of your `IHostManagedPlugin`). Validate a manifest against the host contract
with [`callora plugin test-contract`](/guides/getting-started/plugin-cli#plugin-test-contract).

Wire `registry.json` into the build so it lands beside the DLL:

```xml
<ItemGroup>
  <None Include="registry.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## UI resources

Plugins can contribute UI to a **surface** (e.g. `admin`, `workspace`). Two directories are
involved and the distinction matters:

- **`Resources/app/<surface>/src/`** — the UI *source* (Vue/TypeScript/SCSS). This stays
  with the author and is **not** shipped in the package.
- **`Resources/public/<surface>/`** — the *built* bundle (compiled `main.js`, `main.css`).
  This is what ships and what the host serves.

Only the built bundle is packaged. The `Communication` plugin does exactly this — its
`.csproj` copies `src/Resources/public/**` into the output under `public/`, while the
`src/Resources/app` sources are left out:

```xml
<ItemGroup>
  <Content Include="src/Resources/public/**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <TargetPath>public/%(RecursiveDir)%(Filename)%(Extension)</TargetPath>
  </Content>
</ItemGroup>
```

## The `.csproj`

One reference binds a plugin to the platform:

```xml
<ItemGroup>
  <PackageReference Include="Callora.Plugin.Sdk" Version="0.9.0" />
</ItemGroup>
```

That brings three things: the contract surface you compile against (`Callora.Core`), the
governance analyzers (**CAL0001–CAL0003**), and the build rules that keep platform
assemblies out of your output.

### Why the third one matters

At runtime the plugin load context resolves an assembly by asking whether the process
already has it — not by looking at its name (that used to be a `Callora.*` prefix rule; it
was removed in August 2026, because internal plugins carry the prefix too). Since the host
references `Callora.Core`, the host's copy wins and both sides share one identity for the
contract types.

If a copy of `Callora.Core.dll` sits in your plugin's output folder, the same type can end
up loaded twice — and that fails **when the plugin loads**, not when it builds, with an
error that reads like the host is at fault.

Before the SDK existed, every plugin guarded against this by hand:

```xml
<!-- Don't do this any more -->
<ProjectReference Include="..\..\..\src\Core\Callora.Core.csproj"
                  Private="false" ExcludeAssets="runtime" />
```

One line, easy to drop while restructuring, and nothing goes red when you do. The SDK
carries it on the package edge instead, and adds two MSBuild targets as a net for the case
where you also reference `Callora.Core` directly.

::: warning `CalloraFrameworkAssembly` is for the host, not for plugins
This MSBuild property marks an assembly as *part of the framework*, which relaxes the
governance analyzers. Only the platform's own assemblies set it. The SDK defaults it to
`false` for you — setting it to `true` in a plugin silently disables the contract guard.
:::

## Next steps

- [The `callora` CLI](/guides/getting-started/plugin-cli) — scaffold this layout in one
  command and validate the result.
- [The plugin entry contract](/guides/fundamentals/plugin-entry) — implement
  `IHostManagedPlugin` and register your exports.
- [Backend extensions](/guides/backend-extensions) — expose HTTP APIs and services from a
  plugin.
- [Testing & publishing](/guides/testing-and-publishing) — validate against the contract
  and sign for distribution.
- [.NET API reference](/api/) — the contract types you compile against.
