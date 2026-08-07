# Architecture

Callora is deliberately modeled on Shopware and Symfony: a thin platform kernel that owns
cross-cutting concerns, and plugins that carry all domain logic and wire themselves in
**code-first** (ADR-009). This
page explains the platform model, the runtime that loads plugins, and the governance
boundary that keeps a broadly visible core from becoming an unmanageable contract.

## Host versus plugins

The **host** (`src/Host`, on top of `src/Core`) is a *pure platform*:

- Authentication and RBAC (SuperAdmin is global; Admin is scoped per workspace).
- User and plugin management, including the operator lifecycle API.
- The business-event bus and the background job queue.
- The dynamic plugin-routing surface.

It contains **no** domain logic. Voice, dialing, contact-center flows, themes — every
domain-specific concern — lives in a **plugin** under `custom/plugins` (application
plugins) or `custom/static-plugins` (bundled system plugins such as Communication).

This split is ADR-007. The
practical consequence for you: you never edit the core to add a feature. You write a
plugin and attach it through one of the [documented mechanisms](../guides/backend-extensions.md).

## The three axes

Callora separates three orthogonal concepts. Conflating them is the most common early
modeling mistake.

| Axis | Question it answers | Rough analogue |
| --- | --- | --- |
| **Tenant** | Who pays / who is billed? | The customer account |
| **Workspace** | Which logical system and data set? | A Shopware sales channel's data |
| **Surface** | Which access point, and which page within it? | A sales channel, plus its category tree |

- Data shared across access points → **one workspace, several surfaces**.
- Systems that must stay isolated → **several workspaces**.

Surfaces form a **tree** (ADR-019). A node without a parent is an **application root** and
carries the access — host, access mode, theme, identity provider. A node with a parent is a
**page**: it inherits all of that and overrides only what it needs. Every node may carry a
layout, which is what makes a website with several pages possible without several access
channels.

Inheritance and navigation end at the next root: two nodes under one root are the same
application, two under different roots are not. Only a root assigns an identity provider, so the
session boundary coincides with the application boundary rather than sitting somewhere inside a
tree.

A workspace is, by definition, **not** a front-end. The tenant-facing rendering layer is
the **Surface runtime** (ADR-015),
which is why the former `Callora.Workspace` API package is being renamed to
`Callora.Surface`. Business events and plugin routes are scoped by `WorkspaceKey`
throughout.

## The ALC-based plugin runtime

Every plugin loads into its own **collectible** `AssemblyLoadContext` (ALC), so it can be
unloaded and hot-swapped without restarting the host.

- **`PluginAssemblyLoadContext`** (`src/Core/Application/Plugins/PluginAssemblyLoadContext.cs`)
  extends `AssemblyLoadContext` with `isCollectible: true`. Its `Load` override does two
  things that matter: any assembly named `Callora` or `Callora.*` returns `null` so it
  resolves from the host's default context (shared type identity), and everything else is
  resolved plugin-locally via `AssemblyDependencyResolver`.
- **`RuntimePluginHost`** (`ICalloraPluginRuntime`, in
  `src/Core/Application/Plugins/RuntimePluginHost.cs`) owns install / activate / deactivate
  / uninstall, guarding mutations with a `SemaphoreSlim`.
- **Discovery is one-shot and recursive at startup.**
  `LocalPluginDiscoveryService` (`src/Core/Infrastructure/Startup/`) walks the plugin roots
  for `registry.json` files and reconciles them against the database. There is **no**
  `FileSystemWatcher`; the database is the source of truth for installed / active state.
- **Unload is GC-verified.** On deactivation the host calls the plugin's `StopAsync`,
  drops its exports, calls `AssemblyLoadContext.Unload()`, and then waits (via a
  `WeakReference`) for the ALC to be collected. If the context stays pinned, the plugin is
  marked `UnloadFailed` rather than silently leaking.

### Why the core is shared, not isolated

ALCs isolate *types and versions*, not *capabilities*. Callora deliberately wants a plugin
to be able to decorate and subscribe to a broad surface, and decoration requires **type
identity**: a plugin can only decorate a service whose type it can reference. So `Callora.Core`
(plus `Administration` and `Surface`) is loaded once into the shared default context and
never duplicated into plugin ALCs. Third-party contract assemblies that must be shared
across plugins are unified through **`SharedContractAssemblyRegistry`**
(ADR-012, PLAT-256): they load into
the default context so host and plugins see the same `System.Type` instances.

## The governance boundary

A single, broadly visible core is powerful but dangerous: every public signature risks
becoming a de-facto contract. Callora manages this with **three visibility tiers** and a
set of compile-time guards — the .NET equivalent of Shopware's PHPStan `@internal` /
`@final` / BC-checker.

### The three tiers (ADR-012 §2.2)

| Tier | Meaning | Marker |
| --- | --- | --- |
| **Visible** | Referenceable, but not a sanctioned extension point | (no marker) |
| **Extensible** | May be implemented / contributed to / decorated | `[CalloraExtensible]` |
| **Replaceable** | May be replaced entirely, under deterministic precedence | `[CalloraExtensible(Replaceable)]` |

Two attributes carry the intent, both in `src/Core/Extensibility`:

```csharp
// An official extension point. Mode says what a plugin may do with it.
[CalloraExtensible(ExtensionPointMode.Decoratable, "Wrap to intercept outbound mail")]
public interface IMailSender { /* ... */ }

// Public for technical reasons only — NOT a plugin contract.
[CalloraInternal("Composition root; construct via the host")]
public sealed class SomeInternalWiring { /* ... */ }
```

`ExtensionPointMode` is `Contributable` (additive, the default), `Decoratable`
(wrap via `IServiceDecorator<TService>`), or `Replaceable`. Security-critical host
contributors carry `[HostProtected]` so a plugin export cannot silently supplant them.

### The analyzers (`src/Analyzers`)

The Roslyn analyzers run in-build with `TreatWarningsAsErrors` and
`EnforceCodeStyleInBuild` on (`Directory.Build.props`). Framework assemblies exempt
themselves via the `CalloraFrameworkAssembly=true` MSBuild property; **your plugin does
not set it**, so all four rules apply to plugin code.

| ID | Rule | Severity |
| --- | --- | --- |
| **CAL0001** | Consuming a `[CalloraInternal]` API from outside the framework | Error |
| **CAL0002** | Deriving from / implementing a `[CalloraInternal]` type | Error |
| **CAL0003** | Missing XML docs on the plugin-contract surface (`.Contracts`, `Extensibility`, `[CalloraExtensible]` members) | Error |
| **CAL0004** | An `[ExtensionPointId]` argument that does not reference a `CalloraExtensionPoints` constant | Error |

`CAL0004` turns a mistyped extension-point id into a compile error with IDE completion,
instead of a runtime activation failure. All four link to
ADR-012.

### The PublicAPI baseline

On top of the Callora analyzers, every framework assembly runs Microsoft's
`Microsoft.CodeAnalysis.PublicApiAnalyzers` with a tracked baseline: `PublicAPI.Shipped.txt`
(released surface) and `PublicAPI.Unshipped.txt` (surface added since the last release),
present in `src/Core`, `src/Administration`, `src/Workspace`, and `src/Surface.Rendering`.
Adding or changing a public signature that is not recorded fails the build (`RS0016` /
`RS0017`). This is the mechanism that makes "public core signatures are de-facto contracts"
enforceable rather than aspirational. The workflow is in
[Testing & Publishing](../guides/testing-and-publishing.md#the-publicapi-baseline-workflow).

## How this mirrors Shopware / Symfony

| Shopware / Symfony | Callora |
| --- | --- |
| Bundles wire in code, not by manifest endpoints | Code-first extension wiring (ADR-009) |
| Event subscribers, before/after events | `IBusinessEventListener`, mutable/cancelable events |
| Service decoration / DI decoration | `IServiceDecorator<TService>` per-call proxy |
| PHPStan `@internal` / `@final` / BC-checker | `CAL0001`–`CAL0004` + PublicAPI baseline |
| Trusted, signed marketplace plugins | Trusted-in-process by provenance (ADR-013) |
| Storefront SSR + progressive enhancement | Surface SSR (Nunjucks/Jint) + Vue islands |

The guiding principle (ADR-012) is
**"freedom over protection"**: maximum extensibility on a broad visible surface, with real
internal boundaries reserved for security, compliance, and lifecycle governance.
