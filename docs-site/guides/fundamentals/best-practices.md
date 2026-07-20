# Best Practices

A checklist for shipping a Callora plugin that installs cleanly, survives hot
activate/deactivate, stays inside the contract, and passes review. Each item links to the
deeper guide. Skim it before you open a PR; run it again before you sign for production.

## What you'll learn

- The one MSBuild setting you must **not** flip, and why the analyzers depend on it
- Structural rules the codebase enforces (one type per file, DDD layering)
- Runtime hygiene: safe routes, idempotent jobs, secrets in the right place
- The pre-ship gate: analyzer, `test-contract`, and signing

## Don't set `CalloraFrameworkAssembly`

Leave `CalloraFrameworkAssembly` at its **default of `false`**. It is `true` only for the
platform's own framework assemblies (Core, Administration, Cli, Analyzers), which are
allowed to consume their internal surface. For your plugin, `false` keeps the governance
analyzers active — and those analyzers are what keep you inside the supported contract:

| Diagnostic | Enforces |
| --- | --- |
| **CAL0001** | You may not *consume* a `[CalloraInternal]` API — it's not part of the plugin contract |
| **CAL0002** | You may not *derive from or implement* a `[CalloraInternal]` type — only `[CalloraExtensible]` points are open |
| **CAL0003** | Public types/members on the contract surface (`.Contracts` namespaces, `[CalloraExtensible]`) must carry XML docs |
| **CAL0004** | An `[ExtensionPointId]` argument must reference a `CalloraExtensionPoints` constant, not a raw string |

All four are build-breaking errors. If you flip `CalloraFrameworkAssembly` to `true` to
"make the error go away," you've disabled the very check that tells you when you're binding
to something the platform can change out from under you.

::: warning
Reaching for an API that CAL0001/CAL0002 rejects is a signal, not an obstacle: that type
isn't a contract. Find the exported contract or extension point that does what you need —
see [Exporting extensions](./exporting-extensions) — or file for a new extension point.
:::

→ [Exporting extensions](./exporting-extensions) · [Plugin dependencies](./plugin-dependencies)

## One type per file, DDD layering

The codebase enforces the same structure for the host and every plugin (`ENGINEERING_RULES.md`,
`CODE_STRUCTURE_RULES.md`):

- **One top-level type per file**, file name = type name. No nested types (no `class`
  inside `class`, no helper `record` tucked into another file).
- **DDD layering.** `Domain` (no frameworks) → `Application` (defines ports/interfaces) →
  `Infrastructure` (implements ports) → `Api` (thin controllers delegating to Application).
  Wiring happens only at the composition root — for a plugin, that's `StartAsync`.
- **Small, focused classes;** no silent `catch` blocks; public APIs documented.

→ [The plugin entry class](./plugin-entry) · [Dependency injection](./dependency-injection)

## Route under a namespace you own

When you export an `IApiController`, every `[CalloraRoute]` becomes a real host route.
**Namespace it under a segment you own** (`/api/acme/…`), never under a reserved host
prefix. Reserved prefixes include `/api/auth`, `/api/config`, `/api/plugins`, `/api/jobs`,
`/api/webhooks`, `/api/media`, `/api/notifications`, `/api/tenants`, `/api/users`,
`/api/workspaces`, and more (`ReservedHostRoutePrefixes`). A route that equals or sits under
a reserved prefix is **logged and rejected** at routing refresh — your endpoint silently
won't map.

→ [Exporting extensions](./exporting-extensions#the-catalogue-of-exportable-contracts) · [REST API reference](/reference/rest-api)

## Make jobs idempotent

Background jobs are delivered **at least once** — a retry or a crash-recovery can run the
same `BackgroundJobExecutionContext` twice. Your `IBackgroundJobHandler.ExecuteAsync` must
tolerate that: running a job twice must not double an external effect (send, charge,
provision). Use an idempotency key, check-before-act, or a dedupe record. The
`Attempt` value on the context tells you when you're on a retry.

The same rule applies to `IWorkspaceDataPurgeContributor.PurgeWorkspaceAsync` — purge is
retried on failure, so deleting already-deleted rows must be a no-op.

→ [Events & Jobs](/guides/events-and-jobs) · [Compliance metadata](./compliance-metadata#gdpr-erasure-iworkspacedatapurgecontributor)

## Keep secrets in `ISecretStore`

Never hard-code credentials or read them from a checked-in file. Resolve **`ISecretStore`**
from the context and fetch by name (`GetSecretAsync(name)`); it's backed by environment
variables and configuration, extensible to vault providers. For sensitive values *you*
persist, encrypt them with **`IPluginDataProtector`** (`Protect`/`TryUnprotect`), which
isolates protection per plugin. Config fields declared with the `secret` field type are
encrypted at rest and masked when read back through the operator API.

→ [Plugin configuration](./plugin-configuration#defining-and-setting-config-operator-endpoints)

## Declare `sensitiveFields`

If your plugin emits person-related data through webhooks, list those field names in
`registry.json` under `sensitiveFields`. The platform masks them on the way out. It costs
one array and closes a real data-leak path.

→ [Compliance metadata](./compliance-metadata#sensitivefields-data-minimization-for-webhooks)

## Deterministic startup

`StartAsync` should be **resolve → construct → export → return**. Don't do I/O, don't block,
don't kick off long-running loops there — a slow or throwing `StartAsync` stalls hot
activation. Move deferred work into an `IBackgroundJobHandler` or `IRecurringJobProvider`,
and make `StopAsync` release anything you hold so deactivation is clean.

→ [Exporting extensions](./exporting-extensions#worked-example-exporting-several-contracts)

## Test with the analyzer and `test-contract`

Before you ship:

1. **Build with analyzers on** (`CalloraFrameworkAssembly=false`). A clean build means you
   respected CAL0001–CAL0004.
2. **Run `callora plugin test-contract --assembly <your.dll>`.** It validates that
   `registry.json` parses, has the required fields (`contractVersion`, `schemaVersion`,
   `name`, `pluginId`, `version`, `assemblyFileName`, `entryTypeName`), that
   `assemblyFileName` matches the real DLL, that you reference the right `Callora.Core`
   major version, and that your entry type implements the lifecycle contract, has a public
   parameterless constructor, instantiates, and reports a non-empty id and display name.

→ [Testing & Publishing](/guides/testing-and-publishing) · [Plugin dependencies](./plugin-dependencies#contract-version-gates)

## Sign for production

For anything beyond local development, **sign the plugin**. Callora uses a signed content
manifest (ECDSA P-256 over SHA-256), which hashes every file in the plugin directory —
assembly, `registry.json`, assets, migrations — into a detached `plugin.signature.json`.
Trust is keyed to the **signer fingerprint** (SHA-256 of the public key), not a certificate
chain. Keep the private key **outside** the plugin directory.

→ [Testing & Publishing](/guides/testing-and-publishing)

## Next steps

- Back to the map: **[Plugin fundamentals](./index)**
- The extension mechanism in depth: **[Exporting extensions](./exporting-extensions)**
- The bigger picture: **[Architecture concepts](/concepts/architecture)**
- Ship it: **[Testing & Publishing](/guides/testing-and-publishing)**
