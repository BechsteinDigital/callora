# Plugin Dependencies

A Callora plugin rarely lives alone. It depends on the host's **contract package**, and
often on **abstraction packages** published by another plugin. The example running through
this page is a dialer: it places calls through the Communication plugin's
`ICommunicationChannelRegistry` without ever knowing what SIP is. This page covers how you
declare those dependencies in `registry.json`, how Callora guarantees every plugin sees the
*same* contract types, and how contract versions are gated over time.

## What you'll learn

- The `dependencies` block: declaring contract/abstraction packages with version ranges
- Why all plugins must share **one identity** of a contract assembly (ALC type identity)
- Contract-version gates: `v2` supported, `v1` deprecated, `v0` removed
- Where to read the live support/compatibility matrix

## Declaring dependencies

`dependencies` maps a package (assembly) name to a version range. A dialer's manifest would
declare:

```json
{
  "pluginId": "dialer",
  "version": "0.1.0",
  "requiresCapabilities": ["communication.voice"],
  "dependencies": {
    "Callora.Core": ">=0.9.0",
    "Callora.Plugin.Communication.Abstractions": ">=0.9.0"
  }
}
```

Two categories show up here:

- **The host contract package** — `Callora.Core` defines the interfaces you export and
  consume. You do not reference it directly: `Callora.Plugin.Sdk` brings it, together with
  the analyzers and the build rules that keep platform assemblies out of your output.
- **Abstraction packages** — a *sibling plugin's* published abstractions
  (`Callora.Plugin.Communication.Abstractions`). Depending on these lets you consume that
  plugin's exported services (e.g. `ICommunicationChannelRegistry`) by their real .NET
  types. A plugin's contracts are published publicly even when the plugin itself is not —
  that is what makes building against them possible.

Note the difference between `dependencies` and `requiresCapabilities`. The former is about
*which assemblies and types* you build against; the latter is about *which running
capability* must be present at activation. The dialer needs the Communication abstractions
assembly to **compile**, and a plugin providing the `communication.voice` capability to
**run**.

::: warning
**The ranges are enforced, not documentation.** `PluginDependencyVersionGate` runs at
install time (from `PluginInstaller`) and rejects a plugin whose declared range does not
match the version the host actually provides. Write the range you mean.

Two details worth knowing:

- **Only *resolvable* dependencies are checked.** A dependency the host provides no
  assembly for is skipped here — whether it can be satisfied at all is the activation
  planner's question, not the gate's.
- **An unparseable range is a hard error**, not a shrug. `">= 1.0"` with a space, a typo,
  an empty string: the install is rejected rather than the range ignored.
:::

## One identity per contract assembly

The subtle danger in any plugin system that loads assemblies into separate load contexts:
two plugins can each carry their own copy of `ICommunicationChannelRegistry`, and to the
CLR those are **different types** — a cast between them fails at runtime. Callora prevents
this with `SharedContractAssemblyRegistry`
(`src/Core/Application/Plugins/SharedContractAssemblyRegistry.cs`).

Contract/abstraction assemblies are loaded **exactly once** into the default load context,
so every plugin resolving the same contract sees the **same .NET type identity**. The host
itself never has to reference third-party contracts:

- **First registration wins.** A later registration of the same assembly name is accepted
  only if its **major version matches**. A major-version mismatch throws:
  *"Contract assembly '{name}' is already shared as version {existing}; version {new} has
  an incompatible major version."*
- **Callora host contracts are already shared.** Assemblies named `Callora` or starting
  with `Callora.` are provided by the host's default context and are skipped by the
  registry — you never double-load them.
- **Resolution is major-version-gated.** Asking for a different major version than the one
  registered returns nothing, rather than silently handing back an incompatible type.

::: warning
This is why a **consuming host that references a plugin's abstractions must reference the
same shared abstraction assembly** — the one that defines the contract types — so that ALC
type identity is preserved end-to-end. If the host and the plugin bind against different
copies of `…Communication.Abstractions`, exported services won't cast across the boundary
and the integration breaks at runtime.
:::

## Contract-version gates

Beyond individual assemblies, Callora versions its **plugin contract surface** as a whole,
through the `contractVersion` field in `registry.json`. The runtime installer policy
(`PluginContractVersionPolicy`) is:

| `contractVersion` | Status | Installable? | Behavior |
| --- | --- | --- | --- |
| `v2` | Supported | Yes | Installs cleanly |
| `v1` | Deprecated | Yes, **with warning** | Installs, emits `PLUGIN_CONTRACT_VERSION_DEPRECATED` |
| `v0` | Removed | **No** | Blocked with `PLUGIN_CONTRACT_VERSION_REMOVED` |

Related registry error/warning codes (`PluginRegistryErrorCodes`):

- `PLUGIN_CONTRACT_VERSION_MISSING` — no `contractVersion` field
- `PLUGIN_CONTRACT_VERSION_UNSUPPORTED` — a value not in the policy
- `PLUGIN_CONTRACT_VERSION_DEPRECATED` — installable, warning emitted (currently `v1`)
- `PLUGIN_CONTRACT_VERSION_REMOVED` — installation blocked (currently `v0`)

::: warning
`callora plugin new` scaffolds `"contractVersion": "v1"`, and `callora plugin test-contract`
validates against **`v1`** as the current target. The runtime installer's support matrix already lists `v2` as the *supported* tier
and `v1` as *deprecated-but-installable*. In practice: set `v1` today (it installs and
passes `test-contract`), and expect `v2` to become the target as the contract surface
advances. Watch the deprecation warning as your signal to migrate.
:::

## The live support & compatibility matrix

You don't have to hard-code the table above — the host exposes it (`PluginEndpoints`,
`src/Administration/Api/PluginEndpoints.cs`, permission `PluginRead`):

- **`GET /api/plugins/contracts/support`** — one row per known contract version with its
  status, whether it's installable, and whether it emits a warning:

  ```json
  [
    { "contractVersion": "v2", "supportStatus": "Supported",  "isInstallable": true,  "emitsWarning": false, "message": "Actively supported contract version." },
    { "contractVersion": "v1", "supportStatus": "Deprecated", "isInstallable": true,  "emitsWarning": true,  "message": "Deprecated contract version. Installation is allowed with warning." },
    { "contractVersion": "v0", "supportStatus": "Removed",    "isInstallable": false, "emitsWarning": false, "message": "Removed contract version. Installation is blocked." }
  ]
  ```

- **`GET /api/plugins/contracts/compatibility`** — the same rows joined with the running
  host/core versions, and a `result` of `compatible`, `compatible_with_warning`, or
  `incompatible`.

::: tip
Before shipping, hit `/api/plugins/contracts/support` against your target host to confirm
your `contractVersion` is still installable, and run `callora plugin test-contract` to
catch a mismatch locally. See [Testing & Publishing](/guides/testing-and-publishing).
:::

## Worked dependencies block

A minimal plugin that consumes another plugin's abstractions and needs one running
capability:

```json
{
  "contractVersion": "v1",
  "schemaVersion": "1.0",
  "name": "Acme Callback Scheduler",
  "pluginId": "acme-callback",
  "version": "1.0.0",
  "capabilities": [],
  "requiresCapabilities": ["communication.voice"],
  "dependencies": {
    "Callora.Core": ">=0.9.0",
    "Callora.Plugin.Communication.Abstractions": ">=0.9.0"
  }
}
```

**Expected behavior:** the plugin installs on a host at `contractVersion` `v1` (with the
deprecation warning), provided the host's `Callora.Core` satisfies `>=0.9.0` — the
dependency gate checks that before anything else happens. At activation it can resolve
`ICommunicationChannelRegistry` from the Communication plugin — the same type both sides
compiled against, thanks to the shared assembly registry — but only if a plugin providing
`communication.voice` is active.

## Next steps

- The full manifest field-by-field: **[The registry manifest](./registry-manifest)** · **[Extension manifests reference](/reference/extension-manifests)**
- Capabilities vs. dependencies: **[Capabilities](/guides/capabilities)**
- Consuming another plugin's exports: **[Exporting extensions](./exporting-extensions)**
- Validate before shipping: **[Testing & Publishing](/guides/testing-and-publishing)** · **[REST API reference](/reference/rest-api)**
