# Plugin project layout

A Callora plugin is a normal .NET class-library project plus a `registry.json` manifest.
This guide describes the recommended structure — how to organize code by DDD layers, where
each artifact belongs, and the contract rules your `.csproj` must follow. The layout here is
the one the shipped `Dialer` and `Communication` plugins use.

## What you'll learn

- The recommended folder structure and what each folder holds
- Where `registry.json`, the `.csproj`, and UI resources live
- How source UI (`Resources/app`) differs from the built bundle (`Resources/public`)
- The contract rules: reference `Callora.Core` at compile time only, and do **not** set
  `CalloraFrameworkAssembly`

::: tip Prerequisites
- The [`callora` CLI](/guides/getting-started/plugin-cli), which scaffolds this layout for
  you with `plugin new`.
- Familiarity with the [plugin entry contract](/guides/fundamentals/plugin-entry).
:::

## Recommended structure

Callora's engineering rules favour small, single-responsibility classes — **one public type
per file** — organized by DDD layer. A fuller plugin looks like this:

```
custom/plugins/MyPlugin/
├─ Callora.Plugins.MyPlugin.csproj   # project file
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
the entry type, and the plugin's capabilities. The `Dialer` plugin's manifest:

```json
{
  "contractVersion": "v1",
  "schemaVersion": "1.0",
  "name": "Callora Dialer Plugin",
  "pluginId": "dialer",
  "version": "0.1.0",
  "assemblyFileName": "Callora.Plugins.Dialer.dll",
  "entryTypeName": "Callora.Plugins.Dialer.Application.DialerPlugin",
  "capabilities": [],
  "requiresCapabilities": ["communication.voice"],
  "dependencies": {
    "Callora.Host.PluginContracts": ">=0.1.0",
    "Callora.Plugin.Communication.Abstractions": ">=0.1.0"
  }
}
```

Key fields: `contractVersion` (must be `v1`), `schemaVersion`, `name`, `pluginId`,
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

## The `.csproj` and the contract rules

Two rules keep a plugin correctly bound to the host contract.

**1. Reference `Callora.Core` at compile time only.** The host provides `Callora.Core` at
runtime and the plugin's load context shares its type identity — so the plugin must compile
against it but must not ship it. Use `ExcludeAssets="runtime"` (and `Private="false"` for a
`ProjectReference`):

```xml
<ItemGroup>
  <ProjectReference Include="..\..\..\src\Core\Callora.Core.csproj"
                    Private="false" ExcludeAssets="runtime" />
</ItemGroup>
```

**2. Do NOT set `CalloraFrameworkAssembly`.** This MSBuild property marks an assembly as
*part of the framework*, which relaxes the governance analyzers. Plugins must leave it at
its default (`false`) so the **CAL0001–CAL0004** analyzers enforce the contract: a plugin
that consumes a `[CalloraInternal]` host API, or otherwise breaks the contract, fails the
build. The shipped plugins reference the analyzer package explicitly to get this checking:

```xml
<ProjectReference Include="..\..\..\src\Analyzers\Callora.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

::: warning `CalloraFrameworkAssembly` is for the host, not for plugins
Only the platform's own assemblies (Core, Administration, Workspace, Analyzers, …) set
`CalloraFrameworkAssembly=true`. Setting it in a plugin silently disables the contract
guard — leave it unset.
:::

> **Status:** In-repo plugins reference `Callora.Core` and `Callora.Analyzers` via
> `ProjectReference`. For external plugins these are intended to arrive via NuGet
> (`Callora.Core` carries the analyzer), but that packaging path is being finalized.

## Next steps

- [The `callora` CLI](/guides/getting-started/plugin-cli) — scaffold this layout in one
  command and validate the result.
- [The plugin entry contract](/guides/fundamentals/plugin-entry) — implement
  `IHostManagedPlugin` and register your exports.
- [Backend extensions](/guides/backend-extensions) — expose HTTP APIs and services from a
  plugin.
- [.NET API reference](/api/) — the contract types you compile against.
