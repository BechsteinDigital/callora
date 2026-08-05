# Plugin Contracts

A plugin can publish types that other plugins compile against: an interface, its read models, its
capability keys. Callora shares those across plugin load contexts so every consumer sees the same
.NET type identity, and the host itself never has to reference them.

This page is the build-time path for that. Runtime extension points are in
[Extension Points Reference](./extension-points.md).

## Why a separate assembly

Each plugin is loaded into its own collectible `AssemblyLoadContext` so it can be installed,
updated and removed while the host runs. A consumer that references the plugin assembly directly
gets a second copy of every type in its own context, and `is`/`as` against the producer's instances
fails. Putting the contract in its own assembly and declaring it lets the host load that one
assembly exactly once, into the default context, where both sides resolve to the same types.

## Building one

The contract project is an ordinary library. Keep it dependency-free: a contract that drags EF Core
or ASP.NET into a consumer is not a contract.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <RootNamespace>Acme.Chat.Contracts</RootNamespace>
  </PropertyGroup>
</Project>
```

::: danger The `Callora.` prefix is reserved
Do not name your contract assembly `Callora.something`. That prefix means "the host provides this":
the plugin load context delegates those names to the default context instead of loading them
locally, which only works for assemblies the host application actually references.

A plugin-provided contract carrying the prefix would be refused by the shared registry and absent
from the default context, so it would fail to load the moment your plugin touched one of its types.
The host rejects such a declaration at install time rather than letting it reach that point.
:::

Ship the contract's DLL inside your plugin bundle and declare it in `registry.json`:

```json
{
  "pluginId": "acme.chat",
  "assemblyFileName": "Acme.Chat.dll",
  "capabilities": ["acme.chat.rooms"],
  "contracts": ["Acme.Chat.Contracts.dll"]
}
```

Export the implementation from your plugin's `StartAsync`:

```csharp
context.Export<IAcmeChatService>(new AcmeChatService(...));
```

## Consuming one

A consumer references the contract project or package at build time, declares the dependency, and
resolves the export at runtime. It never references the producing plugin assembly.

```json
{
  "pluginId": "crm",
  "dependencies": {
    "Acme.Chat.Contracts": ">=1.0.0 <2.0.0"
  }
}
```

The declared range is enforced at install time against the version actually pinned. A range the
pinned version does not satisfy fails the install rather than surfacing later as a missing method.

## What a contract change costs

A shared contract is loaded once and stays pinned for the host's lifetime. Replacing it therefore
needs a host restart, while everything else about a plugin stays hot-swappable. Two consequences
worth planning around:

- A second plugin declaring the same contract at a different **patch or minor** version reuses the
  already-loaded one. A different **major** version is refused.
- Announce a contract change before applying it. The catalog below tells you who is affected.

## The catalog

`GET /api/plugins/contracts` (permission `extension.read`) lists what an installation offers:

```json
[
  {
    "assemblyName": "Acme.Chat.Contracts",
    "version": "1.2.0.0",
    "declaringPluginId": "acme.chat",
    "isHostProvided": false,
    "requiresRestartToChange": true,
    "dependents": [
      { "pluginId": "crm", "requiredRange": ">=1.0.0 <2.0.0", "isSatisfied": true }
    ]
  }
]
```

`dependents` is the part that matters before an update: it names every installed plugin bound to the
contract and whether the pinned version still satisfies what it asked for. An entry with
`"isSatisfied": false` names a plugin that is already broken, or that would break if you applied the
change.

## Distribution

How the contract package reaches an external developer's build (a NuGet feed, a source drop, a
project reference in a monorepo) is a deployment question and not fixed by the platform. What the
platform fixes is the shape above: a separately named assembly, declared under `"contracts"`,
depended on by range, and visible in the catalog.
