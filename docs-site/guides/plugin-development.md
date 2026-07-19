# Plugin Development

This page is the anatomy of a Callora plugin: its manifest, its runtime entry contract,
its lifecycle, its project layout, how it hot-loads, and how it is signed.

## `registry.json` — the manifest

Every plugin ships a `registry.json` next to its compiled assembly. Since
ADR-009 it is a **governance
metadata source**, not the extension-wiring source: extension points are attached in code,
and the manifest carries identity, versioning, contract, capabilities, and compliance data.
It is parsed by `JsonPluginPackageRegistryReader`
(`src/Core/Infrastructure/Plugins/`) into `PluginRegistryJsonDto`.

A real manifest, from the bundled Communication plugin
(`custom/static-plugins/Communication/registry.json`):

```json
{
  "contractVersion": "v1",
  "schemaVersion": "1.0",
  "name": "Callora Communication",
  "pluginId": "communication",
  "version": "0.2.0",
  "tier": "system",
  "assemblyFileName": "Callora.Plugin.Communication.dll",
  "entryTypeName": "Callora.Plugin.Communication.Application.CommunicationPlugin",
  "databaseSchema": "plugin_communication",
  "capabilities": [
    "communication.voice"
  ],
  "sensitiveFields": [
    "phoneNumber",
    "callerNumber",
    "calleeNumber"
  ],
  "dependencies": {
    "Callora.Host.PluginContracts": ">=0.1.0",
    "Callora.Plugin.Communication.Abstractions": ">=0.1.0"
  }
}
```

| Field | Required | Purpose |
| --- | --- | --- |
| `contractVersion` | yes | Host-contract version the plugin targets (`v1`, `v2`, …). See [gates](#apiversion-gates). |
| `schemaVersion` | yes | Manifest schema version. |
| `name` | yes | Display name shown by host tooling. |
| `pluginId` | yes | Stable identifier; also the `plugin_<id>` schema and asset root segment. |
| `version` | yes | Plugin SemVer. |
| `assemblyFileName` | yes | The DLL the runtime loads. |
| `entryTypeName` | optional | Full name of the entry class. If omitted, the runtime scans for the first non-abstract `IHostManagedPlugin`. |
| `tier` | optional | `system` (bundled/foundation) or the default application tier. |
| `capabilities` / `requiresCapabilities` | optional | What this plugin provides / depends on. See [Capabilities](capabilities.md). |
| `dependencies` | optional | Contract/abstraction packages with version ranges; unified via `SharedContractAssemblyRegistry`. |

### Compliance metadata

Per Plugin Contract v1 §6, a manifest must
carry compliance metadata — data categories, processing purposes, AI usage and risk class,
required permissions, and retention/deletion hints. The Communication manifest above
declares `sensitiveFields` as part of that surface.

## The v1 runtime contract

The full contract is Plugin Contract v1. A
plugin provides one entry class implementing **`IHostManagedPlugin`**
(`src/Core/Domain/Plugins/Contracts/IHostManagedPlugin.cs`):

```csharp
[CalloraExtensible("Plugin entrypoint — implement to provide a runtime-loadable plugin")]
public interface IHostManagedPlugin
{
    string PluginId { get; }
    string DisplayName { get; }

    ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
```

In `StartAsync` you resolve host services and **export** your extension implementations.
The context (`IHostPluginContext`, `src/Core/Application/Plugins/Contracts/`) gives you a
**curated** service provider — a filtered view exposing published contracts and cross-plugin
exports, not the raw host container — and a single `Export` call:

```csharp
public sealed class CommunicationPlugin : IHostManagedPlugin
{
    public string PluginId => "communication";
    public string DisplayName => "Callora Communication";

    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken ct = default)
    {
        // React to business events:
        context.Export(typeof(IBusinessEventListener), new CallLifecycleListener());

        // Decorate a host service:
        context.Export(typeof(IServiceDecorator<IMailSender>), new CallSummaryMailDecorator());

        // Expose an HTTP API:
        context.Export(typeof(IApiController), new CallsController());

        // Run migrations for this plugin's own schema:
        var factory = (IPluginDbContextFactory<VoipDbContext>)
            context.Services.GetService(typeof(IPluginDbContextFactory<VoipDbContext>))!;
        await factory.MigrateAsync(ct);
    }

    public ValueTask StopAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
}
```

The host resolves your exports through **`ICalloraPluginCatalog`** (`TryGetExport`,
`GetExports`, `GetOwnedExports`). See [Backend Extensions](backend-extensions.md) for what
each contract does.

### API/version gates

Per Plugin Contract v1 §3, a plugin must be
major-compatible with the host contracts. `v2` is supported, `v1` is deprecated
(installable, with a host warning), and `v0` is removed (rejected with
`PLUGIN_CONTRACT_VERSION_REMOVED`). Clients can read the matrix from
`GET /api/plugins/contracts/support`.

## Lifecycle

The state model is `Installed → Active → Inactive` (`PluginInstallationState`,
`src/Core/Domain/Plugins/`), with `Uninstalled` as the terminal state. The **database is
the source of truth** — discovery reconciles the filesystem *to* the database, never the
other way. Operations are driven by `IPluginLifecycleService`
(`src/Core/Application/Lifecycle/`):

| Operation | Effect |
| --- | --- |
| `install` | Read `registry.json`, verify the signature and contract version, record `Installed`. |
| `activate` | Load the assembly into a fresh ALC, call `StartAsync`, register exports, mark `Active`. |
| `deactivate` | Call `StopAsync`, drop exports, unload the ALC, mark `Inactive`. |
| `uninstall` | Remove from the registry. |

Each transition writes an audit event. The operator API (`src/Administration/Api/PluginEndpoints.cs`)
exposes these under `/api/plugins`, all behind RBAC:

```http
POST   /api/plugins/install            # PluginCreate
POST   /api/plugins/install/nuget      # PluginCreate
POST   /api/plugins/{pluginId}/activate    # PluginExecute
POST   /api/plugins/{pluginId}/deactivate  # PluginExecute
DELETE /api/plugins/{pluginId}             # PluginDelete
GET    /api/plugins                     # PluginRead
GET    /api/plugins/signature-report    # PluginRead — re-verifies all installed signatures
```

## Hot-loading

Activate and deactivate happen **live, without a host restart**. Because each plugin owns a
collectible ALC, activation loads its assembly on demand and deactivation unloads it (with
the GC-verified check described in [Architecture](../concepts/architecture.md#the-alc-based-plugin-runtime)).
Newly exported event listeners and service decorators take effect on the **next** publish
or the **next** call; plugin routes are added to and removed from the routing table on
activation and deactivation. There is nothing to pin and nothing to restart.

## Project layout

A typical application plugin (see `custom/plugins/Dialer`) is layered per the
Engineering Rules (DDD: `Domain`, `Application`,
`Infrastructure`, small focused types, one type per file):

```text
custom/plugins/MyPlugin/
├── registry.json                 # manifest (identity, capabilities, compliance)
├── Callora.Plugins.MyPlugin.csproj
├── src/
│   ├── Domain/                   # entities, value objects, domain contracts
│   ├── Application/
│   │   ├── MyPlugin.cs           # IHostManagedPlugin entry class
│   │   ├── Persistence/          # DbContext + Migrations (plugin_<id> schema)
│   │   ├── Admin/                # AdminApiController implementations
│   │   └── Events/               # IBusinessEventListener implementations
│   ├── Infrastructure/           # host-facing implementation details
│   └── Resources/
│       ├── app/workspace/src/    # surface plugin: main.ts + .vue (see admin-extensions)
│       └── public/workspace/     # built IIFE bundle (main.js/main.css) — published
```

Do **not** set `CalloraFrameworkAssembly` in a plugin project; leaving it at its default
(`false`) is what makes the `CAL0001`–`CAL0004` analyzers enforce the plugin contract on
your code.

## Signing — the content manifest

Callora's trust model is **trusted-in-process by provenance**
(ADR-013). There is no runtime
sandbox; trust is established by **who signed the package**. Because Authenticode is broken
on Linux, Callora uses a signed **content manifest** instead of code signing.

### What the manifest covers

`PluginSignatureManifest` (`src/Core/Application/Plugins/Signing/`) records a SHA-256 hash
of **every** file in the plugin directory (except the signature file itself), plus the
signer's fingerprint, all over an **ECDSA-P256 / SHA-256** signature:

```csharp
public sealed record PluginSignatureManifest(
    string SchemaVersion,                       // "1.0"
    string PluginId,
    string Version,
    string Algorithm,                           // "ecdsa-p256-sha256"
    string SignerFingerprint,                   // SHA-256 of the public key's SubjectPublicKeyInfo
    IReadOnlyList<PluginSignatureFileHash> Files,
    string? Signature);                          // Base64 ECDSA signature
```

Trust is a **public-key fingerprint** — the SHA-256 of the key's `SubjectPublicKeyInfo`.
The verifier (`ManifestSignaturePluginPackageVerifier`) recomputes every file hash, checks
that no un-manifested file was injected, resolves the signer's public key from the trust
store by fingerprint, and verifies the ECDSA signature. Fingerprints and content hashes can
be revoked, and unsigned plugins are rejected unless `AllowUnsignedPlugins` is explicitly on.

### Signing a plugin with the CLI

```bash
callora plugin sign \
  --plugin ./custom/plugins/MyPlugin \
  --key    ./keys/publisher-private.pem \
  --out    ./custom/plugins/MyPlugin/plugin.signature.json   # optional; this is the default
```

- `--plugin` is the directory containing `registry.json`.
- `--key` is an ECDSA P-256 private key in PEM format — keep it **outside** the plugin
  directory so it is never part of the hashed content.
- The command enumerates and hashes the package, builds the manifest, signs its canonical
  form, and writes `plugin.signature.json` into the plugin directory.

The end-to-end publishing flow — configuring trusted signers on the host and the signature
report — is in [Testing & Publishing](testing-and-publishing.md#publishing-a-plugin).
