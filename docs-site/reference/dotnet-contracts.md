# .NET contracts

A plugin is a .NET assembly that builds against Callora's compiled boundary. The
[.NET API reference](/api/), generated from the platform's XML
documentation, is the authoritative catalogue of the public types and members. This
page explains the *boundary* around that catalogue: what a plugin may and may not
consume, and how the boundary is enforced at compile time.

## The generated reference

Start at the [.NET API reference](/api/). It lists every public type and
member with its documentation. The types a plugin author most needs live in the
plugin-contract and communication namespaces — for example:

- `ICalloraRuntimePlugin` / `IHostManagedPlugin` — the runtime entry your plugin
  implements.
- `ICalloraPluginRuntime`, `ICalloraPluginCatalog`, `ICalloraPluginContext` — the
  host services provided to a plugin.
- The `Callora.Contracts.Communication` namespace — the voice/call contracts
  (`ICall`, `ICallAudioStream`, `ICommunicationChannel`, and related event types).

## The v1 plugin contract

The plugin contract is specified in
`docs/modules/PLUGIN_CONTRACT_V1.md`.
It is host-centric and covers:

- **Lifecycle.** The mandatory host operations `install`, `activate`,
  `deactivate`, `uninstall`, and the state model `Installed` / `Active` /
  `Inactive`.
- **Runtime contracts.** A plugin provides a runtime entry class implementing
  `ICalloraRuntimePlugin` (or the legacy `IHostManagedPlugin`); the host provides
  the runtime, catalog, and context services above.
- **Version gates.** A plugin must be major-compatible with the host contracts.
  Support status: **v2** supported, **v1** deprecated (installs with a warning),
  **v0** removed (installation is rejected with
  `PLUGIN_CONTRACT_VERSION_REMOVED`). The support status is queryable at
  `GET /api/plugins/contracts/support`.
- **Security and trust.** Deployment policy, compatibility checks at install, and
  audit events for lifecycle operations. Package integrity is enforced by the
  signed content manifest — see [Extension manifests](extension-manifests.md).

## `[CalloraInternal]` — what plugins may consume

Not every public type is part of the plugin contract. A public type or member
marked `[CalloraInternal]` is host-internal: it is technically reachable but not
part of the supported surface, and plugins must not consume it. The attribute is
defined in `Callora.Core.Extensibility` (`CalloraInternalAttribute`).

Practically: if a type is public and **not** marked `[CalloraInternal]`, it is
part of the contract you may build against. If it is marked `[CalloraInternal]`,
treat it as off-limits — the compiler will flag consumption (see the analyzers
below).

## The `PublicAPI` baseline files

Each shipping module carries Roslyn `PublicAPI` baseline files that pin its public
surface:

- `src/Core/PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
- `src/Administration/PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
- `src/Workspace/PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`
- `src/Surface.Rendering/PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`

`PublicAPI.Shipped.txt` records the released public surface; new additions land in
`PublicAPI.Unshipped.txt` first. Any public API change that is not reflected in
these files is a build warning, so the baselines act as a review gate against
accidental surface changes — the .NET equivalent of a BC checker.

## The governance analyzers

The `Callora.Analyzers` project ships Roslyn analyzers that enforce the boundary
at compile time:

| ID | Enforces |
| --- | --- |
| `CAL0001` | Consumption of a `[CalloraInternal]` type or member (`CalloraInternalConsumptionAnalyzer`). |
| `CAL0002` | Inheriting from / implementing a `[CalloraInternal]` type via the base list (`CalloraInternalConsumptionAnalyzer`). |
| `CAL0003` | A public type/member on the contract surface missing required documentation (`CalloraContractDocumentationAnalyzer`). |
| `CAL0004` | Extension-point ID usage (`CalloraExtensionPointIdAnalyzer`), paired with `[CalloraExtensible]`. |

The policy is *deny-internal*: consuming host-internal surface is an error, while
the rest of the public surface is open to plugins.

## How a plugin author reads the contract

1. Implement `ICalloraRuntimePlugin` and follow the lifecycle in
   `PLUGIN_CONTRACT_V1.md`.
2. Look up the types you need in the [.NET API reference](/api/).
3. Build against public types **not** marked `[CalloraInternal]`. If you
   accidentally reach for an internal one, `CAL0001`/`CAL0002` will stop the
   build.
4. Declare a compatible contract version in `registry.json` (see
   [Extension manifests](extension-manifests.md)); the host gates installation on
   it.
