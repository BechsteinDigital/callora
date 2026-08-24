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

::: info How the name of your contract assembly is treated
It isn't. Name it whatever fits your product — including `Callora.something`, which the
first-party plugins themselves use ([ADR-025](https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-025-interne-plugins-im-callora-namensraum.md)).

Until August 2026 the `Callora.` prefix *was* reserved: any assembly whose name began with it was
delegated straight to the default context. The prefix turned out to be the wrong carrier for that
decision, and wrong in both directions — too broad, because an internal plugin shipping
`Callora.Plugin.Chat.Abstractions` had it sent to a context that did not have it, failing on first
use of any of its types; and too narrow, because a plugin declaring
`Microsoft.Extensions.Logging.Abstractions` under `contracts` got its own copy loaded next to the
host's, with no check at all.

Resolution now asks the question the prefix was standing in for, and asks it of every name alike:

1. **The shared contract registry** — a contract another plugin already declared. This comes first
   so the second plugin to declare a shared contract does not mistake it for host-provided; the
   registry is also the only step that enforces major-version compatibility.
2. **What the host actually provides** — if the process already has the assembly, it owns it: the
   registry records the name and does not load a second copy. This is what keeps `Callora.Core`
   resolving to the host's copy, without a maintained list of names.
3. **Plugin-local** — everything else, loaded into your isolated context.

So a contract only you ship stays yours, and one the host provides stays the host's, whatever
either is called.
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

### The platform's own surface has a middle rung

The same discipline applies to the surface *you* build against, and the platform now has a
way to say it. A member marked `[CalloraDeprecated]` still works and warns
([`CAL0005`](/reference/analyzer-rules#cal0005-using-a-deprecated-callora-extension-surface))
in your build, naming when it was announced, which contract version it stops working in, and
what to use instead.

The stated version is a **promise**: the member survives every release until then, and the
extension-surface gate refuses an earlier removal. So a `CAL0005` warning is not urgent — it
is a deadline you can plan against, which is exactly what the previous two-state contract
could not give you.

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
