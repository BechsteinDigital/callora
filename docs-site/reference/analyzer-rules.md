# Analyzer Rules

Callora ships Roslyn governance analyzers (`Callora.Analyzers`) that enforce the
plugin-contract boundary at compile time — the .NET equivalent of Shopware's
PHPStan `@internal`/`@final` and BC checks. This page catalogues every rule:
`CAL0001`, `CAL0002`, `CAL0003`, and `CAL0004`.

Every rule lives in category **`Callora.Extensibility`**, has default severity
**Error**, is **enabled by default**, and links to
[ADR-012 (Ein Core, Extensibility)](https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-012-Ein-Core-Extensibility.md).

## When the rules enforce

These analyzers gate **plugin/consumer** compilations, not the framework itself.
The distinction is a compiler-visible MSBuild property:

```xml
<PropertyGroup>
  <!-- Framework assemblies (Core / Administration / Workspace / CLI) set this true -->
  <!-- A plugin project leaves it at the default, false — so the contract is enforced. -->
  <CalloraFrameworkAssembly>false</CalloraFrameworkAssembly>
</PropertyGroup>
```

- **`CalloraFrameworkAssembly=true`** → the compilation is treated as a framework
  assembly and `CAL0001`/`CAL0002` are skipped entirely (framework code
  legitimately consumes its own internal surface).
- **Default (`false`)** → a plugin project. `CAL0001`, `CAL0002`, and `CAL0004`
  enforce the boundary against the framework's marked surface. Consuming or
  deriving from a marked symbol declared **in the same assembly** is always
  allowed — a plugin's own internals are its own business.

If the marker attribute assembly (`Callora.Core.Extensibility`) is not
referenced, the corresponding analyzers have nothing to resolve and stay silent.

## The governance markers (REV2 §7)

| Marker | Meaning | Effect |
| --- | --- | --- |
| `[CalloraInternal]` (`CalloraInternalAttribute`) | Public for technical reasons only; **not** a stable plugin contract. Optional `Reason` string. | Consuming it (`CAL0001`) or deriving from it (`CAL0002`) from outside the framework is an error. |
| `[CalloraExtensible]` (`CalloraExtensibleAttribute`) | A sanctioned extension point plugins may implement/consume. | Places the symbol on the documented contract surface (`CAL0003`). |
| `[ExtensionPointId]` (`ExtensionPointIdAttribute`) | Marks a parameter that carries an extension-point id. | A raw string literal there triggers `CAL0004`. |
| `[HostProtected]` (`HostProtectedAttribute`, `internal`) | An extension point the host keeps precedence over. | Referenced by `ExtensionPointMode`; not itself a plugin-facing analyzer target. |

## Rules at a glance

| ID | Title | Category | Severity | Enforced in | Flags |
| --- | --- | --- | --- | --- | --- |
| `CAL0001` | Consuming a `[CalloraInternal]` API from outside the framework | `Callora.Extensibility` | Error | Plugin compilations | Calling/referencing a `[CalloraInternal]` type or member (incl. via generic type arguments, `typeof`, arrays). |
| `CAL0002` | Deriving from or implementing a `[CalloraInternal]` type | `Callora.Extensibility` | Error | Plugin compilations | A base type or implemented interface in the declared base list that is `[CalloraInternal]`. |
| `CAL0003` | Missing XML documentation on the plugin contract surface | `Callora.Extensibility` | Error | Any compilation | A public contract-surface symbol without XML docs. |
| `CAL0004` | Extension-point id must reference a `CalloraExtensionPoints` constant | `Callora.Extensibility` | Error | Any compilation (marker present) | A string literal passed to an `[ExtensionPointId]` parameter. |

---

## CAL0001 — Consuming a `[CalloraInternal]` API from outside the framework

**What it flags.** Any use of a type or member marked `[CalloraInternal]` (or
nested inside a marked type) from a non-framework compilation. Covered operations:
method invocations, object creation, property/field/event references, method
references (delegates), and `typeof`. Generic type arguments are unwrapped, so
`new List<Marked>()`, `typeof(List<Marked>)`, `Factory.Create<Marked>()`, and
marked types inside arrays are all caught. Declarations that expose a marked type
in a signature (method return/parameters, property/field/event types) are flagged
too. Each expression yields at most one diagnostic per distinct culprit.

**Message.** `'{symbol}' is marked [CalloraInternal] and is not part of the
Callora plugin contract; it must not be consumed outside Callora framework
assemblies` — followed by the marker's `Reason`, if one was given.

**Why.** Types and members marked `[CalloraInternal]` are visible for technical
reasons only and are not a stable contract (REV2 §7.1). Plugins must extend
Callora through documented extension points, not by reaching into internal APIs
whose shape can change without notice.

**How to fix / avoid.**

- Use the documented extension points and contract types instead of the internal
  symbol. See [Exporting extensions](/guides/fundamentals/exporting-extensions)
  and [Best practices](/guides/fundamentals/best-practices).
- If the internal type is genuinely the only way to accomplish something, that is
  a gap to raise with the platform — not to work around.
- Framework-internal code that must consume the surface sets
  `CalloraFrameworkAssembly=true` (not applicable to plugins).

---

## CAL0002 — Deriving from or implementing a `[CalloraInternal]` type

**What it flags.** A named type whose **directly declared** base list contains a
`[CalloraInternal]` base class or implemented interface. Only what the plugin
author actually wrote is a violation; interfaces inherited transitively (not
authored by the plugin) are not flagged.

**Message.** `'{type}' is marked [CalloraInternal] and is not an extension point;
plugins must not derive from or implement it` — plus the marker's `Reason`, if
present.

**Why.** `[CalloraInternal]` types are not sanctioned extension points. The
inheritance vector is separate from the member-usage vector `CAL0001` covers, so
it gets its own diagnostic. Plugins extend Callora only through `[CalloraExtensible]`
types or other documented mechanisms (REV2 §7).

**How to fix / avoid.** Implement the sanctioned extension interface (look for the
`[CalloraExtensible]` marker or a `.Contracts` namespace type) instead of the
internal type. See [Architecture](/concepts/architecture) for the governance
boundary and [Plugin entry](/guides/fundamentals/plugin-entry) for the
contract-facing entrypoint (`IHostManagedPlugin`).

::: tip
`CAL0001` and `CAL0002` share one analyzer (`CalloraInternalConsumptionAnalyzer`)
and both skip framework assemblies. `CAL0002` is the inheritance guard; `CAL0001`
covers every other usage.
:::

---

## CAL0003 — Missing XML documentation on the plugin contract surface

**What it flags.** A public, source-declared symbol on the **contract surface**
that has no XML documentation. A symbol is on the contract surface when any of:

- its namespace ends in `.Contracts`, **or**
- it is (nested in) an `Extensibility` namespace, **or**
- the symbol or an enclosing type carries `[CalloraExtensible]`.

"Public" is checked structurally: the symbol's declared accessibility is public
**and** every enclosing type is public (so interface members inside a non-public
interface stay off the surface). Implicitly declared members (record ceremony,
enum backing fields) and property/event accessors are covered by their owning
declaration and not flagged separately. Documentation counts when the XML
contains `<summary>`, `<param>` (positional records), or `<inheritdoc/>`.

**Message.** `'{symbol}' is on the Callora plugin contract surface and must have
XML documentation`.

**Why.** The contract surface is exactly what a plugin author reads to write a
plugin, so it must stay documented. This is the .NET equivalent of enforcing docs
on a hand-picked API package — something the built-in `CS1591` cannot do, because
a compiler warning cannot be escalated from `none` to `error` for a scattered
subset of files (REV2 §7). The internal public surface (`[CalloraInternal]`) is
deliberately out of scope.

**How to fix / avoid.** Add a `<summary>` (or `<inheritdoc/>` where inheriting an
interface's docs) to the flagged type or member. A plugin project scaffolded by
`callora plugin new` already sets `GenerateDocumentationFile=true`.

> **Status (known gap, from the analyzer's own remarks):** a few consumption
> contracts live outside a `.Contracts` namespace (e.g. `ICalloraPluginCatalog`,
> `ICalloraPluginRuntime`, `IHostApplicationEventSubscriber<T>`) and are
> documented but not enforced by this rule. The durable fix is to move them into
> a `.Contracts` namespace rather than tag them `[CalloraExtensible]`.

See [Compliance metadata](/guides/fundamentals/compliance-metadata) and
[Best practices](/guides/fundamentals/best-practices).

---

## CAL0004 — Extension-point id must reference a `CalloraExtensionPoints` constant

**What it flags.** A **string literal** (compile-time constant) passed as an
argument to a parameter marked `[ExtensionPointId]`. Two cases, distinguished by
whether the literal matches a known id:

- Unknown id → `"{id}" is not a known Callora extension-point id; use a
  CalloraExtensionPoints constant`.
- Known id hard-coded as a string → `Use a CalloraExtensionPoints constant
  instead of the raw extension-point id "{id}"`.

A reference to a `CalloraExtensionPoints` constant is the sanctioned form and is
never reported. A **dynamic** (non-constant) value is allowed — the analyzer only
judges what it can see at compile time.

**Known ids** are collected from the `const string` fields of
`Callora.Core.Domain.Extensions.CalloraExtensionPoints`. At the time of writing:

| Constant | Id |
| --- | --- |
| `WorkspaceNavigationMain` | `workspace.navigation.main` |
| `WorkspaceThemeDefinition` | `workspace.theme.definition` |
| `WorkspaceThemeSettings` | `workspace.theme.settings` |
| `AdminNavigationMain` | `admin.navigation.main` |
| `AdminApiRoute` | `admin.api.route` |

**Why.** Extension-point ids are identified by the `[ExtensionPointId]` parameter
marker and must come from `CalloraExtensionPoints` constants, so a mistyped or
unknown id is a compile error with IDE completion rather than a runtime
activation failure (REV2 §8.2).

**How to fix / avoid.** Replace the string literal with the corresponding
`CalloraExtensionPoints` constant:

```csharp
// Flagged (CAL0004):
builder.AddNavigation("workspace.navigation.main", …);

// Correct:
builder.AddNavigation(CalloraExtensionPoints.WorkspaceNavigationMain, …);
```

See [Exporting extensions](/guides/fundamentals/exporting-extensions).

---

## Related reference

- [.NET contracts](/reference/dotnet-contracts) — the compiled boundary and the
  `[CalloraInternal]` / `PublicAPI` baseline the analyzers back.
- [Architecture](/concepts/architecture) — the governance boundary in context.
- The generated [.NET API reference](/api/) — full member lists for the
  contract-surface types.
